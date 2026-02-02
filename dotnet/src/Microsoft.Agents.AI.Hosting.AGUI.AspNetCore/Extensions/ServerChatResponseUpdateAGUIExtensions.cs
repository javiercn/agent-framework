// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AGUI.Protocol;
using Microsoft.Extensions.AI;

#pragma warning disable MEAI001 // Experimental API - FunctionApprovalRequestContent, UserInputRequestContent

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.Extensions;

internal static class ServerChatResponseUpdateAGUIExtensions
{
    private static readonly MediaTypeHeaderValue? s_jsonPatchMediaType = new("application/json-patch+json");
    private static readonly MediaTypeHeaderValue? s_json = new("application/json");

    public static async IAsyncEnumerable<BaseEvent> AsAGUIEventStreamAsync(
        this IAsyncEnumerable<ChatResponseUpdate> updates,
        string threadId,
        string runId,
        JsonSerializerOptions jsonSerializerOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        bool runStartedEmitted = false;
        bool runFinishedEmitted = false;
        string? currentMessageId = null;
        string? currentReasoningMessageId = null;
        string? reasoningSessionId = null;
        await foreach (var chatResponse in updates.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            // Check if RawRepresentation contains an AG-UI event - emit it directly.
            // The BaseEvent may be nested inside an AgentResponseUpdate's RawRepresentation
            // (when AsChatResponseUpdate wraps an AgentResponseUpdate that had a BaseEvent).
            if (ExtractBaseEvent(chatResponse.RawRepresentation) is BaseEvent rawEvent)
            {
                // Track lifecycle events to avoid duplicate emissions
                if (rawEvent is RunStartedEvent)
                {
                    runStartedEmitted = true;
                }
                else if (rawEvent is RunFinishedEvent)
                {
                    runFinishedEmitted = true;
                }
                else if (!runStartedEmitted)
                {
                    // Emit RunStartedEvent before any other event if not explicitly provided
                    runStartedEmitted = true;
                    yield return new RunStartedEvent
                    {
                        ThreadId = threadId,
                        RunId = runId
                    };
                }

                yield return rawEvent;
                continue;
            }

            // Emit RunStartedEvent automatically if not explicitly provided
            if (!runStartedEmitted)
            {
                runStartedEmitted = true;
                yield return new RunStartedEvent
                {
                    ThreadId = threadId,
                    RunId = runId
                };
            }

            if (chatResponse is { Contents.Count: > 0 } &&
                chatResponse.Contents[0] is TextContent &&
                !string.Equals(currentMessageId, chatResponse.MessageId, StringComparison.Ordinal))
            {
                // End reasoning events if we're transitioning to regular text content
                if (reasoningSessionId is not null)
                {
                    if (currentReasoningMessageId is not null)
                    {
                        yield return new ReasoningMessageEndEvent
                        {
                            MessageId = currentReasoningMessageId
                        };
                        currentReasoningMessageId = null;
                    }
                    yield return new ReasoningEndEvent
                    {
                        MessageId = reasoningSessionId
                    };
                    reasoningSessionId = null;
                }

                // End the previous message if there was one
                if (currentMessageId is not null)
                {
                    yield return new TextMessageEndEvent
                    {
                        MessageId = currentMessageId
                    };
                }

                // Start the new message
                yield return new TextMessageStartEvent
                {
                    MessageId = chatResponse.MessageId!,
                    Role = chatResponse.Role!.Value.Value
                };

                currentMessageId = chatResponse.MessageId;
            }

            // Emit text content if present
            if (chatResponse is { Contents.Count: > 0 } && chatResponse.Contents[0] is TextContent textContent &&
                !string.IsNullOrEmpty(textContent.Text))
            {
                yield return new TextMessageContentEvent
                {
                    MessageId = chatResponse.MessageId!,
                    Delta = textContent.Text
                };
            }

            // Emit tool call events and tool result events
            if (chatResponse is { Contents.Count: > 0 })
            {
                foreach (var content in chatResponse.Contents)
                {
                    if (content is FunctionCallContent functionCallContent)
                    {
                        yield return new ToolCallStartEvent
                        {
                            ToolCallId = functionCallContent.CallId,
                            ToolCallName = functionCallContent.Name,
                            ParentMessageId = chatResponse.MessageId
                        };

                        yield return new ToolCallArgsEvent
                        {
                            ToolCallId = functionCallContent.CallId,
                            Delta = JsonSerializer.Serialize(
                                functionCallContent.Arguments,
                                jsonSerializerOptions.GetTypeInfo(typeof(IDictionary<string, object?>)))
                        };

                        yield return new ToolCallEndEvent
                        {
                            ToolCallId = functionCallContent.CallId
                        };
                    }
                    else if (content is FunctionResultContent functionResultContent)
                    {
                        yield return new ToolCallResultEvent
                        {
                            MessageId = chatResponse.MessageId,
                            ToolCallId = functionResultContent.CallId,
                            Content = SerializeResultContent(functionResultContent, jsonSerializerOptions) ?? "",
                            Role = AGUIRoles.Tool
                        };
                    }
                    else if (content is TextReasoningContent reasoningContent)
                    {
                        // Emit reasoning events for reasoning content (new REASONING_* events)
                        if (reasoningSessionId is null)
                        {
                            reasoningSessionId = Guid.NewGuid().ToString("N");
                            yield return new ReasoningStartEvent
                            {
                                MessageId = reasoningSessionId
                            };
                        }

                        // Start a new reasoning message if needed
                        string reasoningMsgId = chatResponse.MessageId ?? Guid.NewGuid().ToString("N");
                        if (currentReasoningMessageId != reasoningMsgId)
                        {
                            // End previous reasoning message if any
                            if (currentReasoningMessageId is not null)
                            {
                                yield return new ReasoningMessageEndEvent
                                {
                                    MessageId = currentReasoningMessageId
                                };
                            }

                            currentReasoningMessageId = reasoningMsgId;
                            yield return new ReasoningMessageStartEvent
                            {
                                MessageId = currentReasoningMessageId,
                                Role = AGUIRoles.Assistant
                            };
                        }

                        // Emit reasoning content
                        if (!string.IsNullOrEmpty(reasoningContent.Text))
                        {
                            yield return new ReasoningMessageContentEvent
                            {
                                MessageId = currentReasoningMessageId,
                                Delta = reasoningContent.Text
                            };
                        }
                    }
                    else if (content is DataContent dataContent)
                    {
                        if (MediaTypeHeaderValue.TryParse(dataContent.MediaType, out var mediaType) && mediaType.Equals(s_json))
                        {
                            // State snapshot event
                            yield return new StateSnapshotEvent
                            {
#if !NET
                                Snapshot = (JsonElement?)JsonSerializer.Deserialize(
                                dataContent.Data.ToArray(),
                                jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)))
#else
                                Snapshot = (JsonElement?)JsonSerializer.Deserialize(
                                dataContent.Data.Span,
                                jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)))
#endif
                            };
                        }
                        else if (mediaType is { } && mediaType.Equals(s_jsonPatchMediaType))
                        {
                            // State snapshot patch event must be a valid JSON patch,
                            // but its not up to us to validate that here.
                            yield return new StateDeltaEvent
                            {
#if !NET
                                Delta = (JsonElement?)JsonSerializer.Deserialize(
                                dataContent.Data.ToArray(),
                                jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)))
#else
                                Delta = (JsonElement?)JsonSerializer.Deserialize(
                                dataContent.Data.Span,
                                jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)))
#endif
                            };
                        }
                        else
                        {
                            // Text content event
                            yield return new TextMessageContentEvent
                            {
                                MessageId = chatResponse.MessageId!,
#if !NET
                                Delta = Encoding.UTF8.GetString(dataContent.Data.ToArray())
#else
                                Delta = Encoding.UTF8.GetString(dataContent.Data.Span)
#endif
                            };
                        }
                    }
                    else if (content is FunctionApprovalRequestContent approvalRequest)
                    {
                        // Emit RunFinishedEvent with interrupt for function approval
                        runFinishedEmitted = true;
                        yield return new RunFinishedEvent
                        {
                            ThreadId = threadId,
                            RunId = runId,
                            Outcome = RunFinishedOutcome.Interrupt,
                            Interrupt = ServerInterruptContentExtensions.ToAGUIInterrupt(approvalRequest, jsonSerializerOptions)
                        };
                    }
                    else if (content is UserInputRequestContent userInputRequest && content is not FunctionApprovalRequestContent)
                    {
                        // Emit RunFinishedEvent with interrupt for user input request
                        runFinishedEmitted = true;
                        yield return new RunFinishedEvent
                        {
                            ThreadId = threadId,
                            RunId = runId,
                            Outcome = RunFinishedOutcome.Interrupt,
                            Interrupt = ServerInterruptContentExtensions.ToAGUIInterrupt(userInputRequest)
                        };
                    }
                }
            }
        }

        // End any remaining reasoning events
        if (reasoningSessionId is not null)
        {
            if (currentReasoningMessageId is not null)
            {
                yield return new ReasoningMessageEndEvent
                {
                    MessageId = currentReasoningMessageId
                };
            }
            yield return new ReasoningEndEvent
            {
                MessageId = reasoningSessionId
            };
        }

        // End the last message if there was one
        if (currentMessageId is not null)
        {
            yield return new TextMessageEndEvent
            {
                MessageId = currentMessageId
            };
        }

        // Emit RunStartedEvent if no updates were processed (empty stream)
        if (!runStartedEmitted)
        {
            yield return new RunStartedEvent
            {
                ThreadId = threadId,
                RunId = runId
            };
        }

        // Emit RunFinishedEvent automatically if not explicitly provided
        if (!runFinishedEmitted)
        {
            yield return new RunFinishedEvent
            {
                ThreadId = threadId,
                RunId = runId,
            };
        }
    }

    private static string? SerializeResultContent(FunctionResultContent functionResultContent, JsonSerializerOptions options)
    {
        return functionResultContent.Result switch
        {
            null => null,
            string str => str,
            JsonElement jsonElement => jsonElement.GetRawText(),
            _ => JsonSerializer.Serialize(functionResultContent.Result, options.GetTypeInfo(functionResultContent.Result.GetType())),
        };
    }

    /// <summary>
    /// Recursively extracts a BaseEvent from a RawRepresentation chain.
    /// The BaseEvent may be nested inside an AgentResponseUpdate that was wrapped
    /// by AsChatResponseUpdate when the original AgentResponseUpdate had RawRepresentation = BaseEvent.
    /// </summary>
    private static BaseEvent? ExtractBaseEvent(object? rawRepresentation)
    {
        if (rawRepresentation is BaseEvent baseEvent)
        {
            return baseEvent;
        }

        // On server, AgentResponseUpdate may wrap a BaseEvent in RawRepresentation
        if (rawRepresentation is AgentResponseUpdate agentUpdate)
        {
            return ExtractBaseEvent(agentUpdate.RawRepresentation);
        }

        return null;
    }
}
