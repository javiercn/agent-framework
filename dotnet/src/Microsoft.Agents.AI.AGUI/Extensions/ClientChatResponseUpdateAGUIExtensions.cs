// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AGUI.Protocol;
using Microsoft.Extensions.AI;

#pragma warning disable MEAI001 // Experimental API - FunctionApprovalRequestContent, UserInputRequestContent

namespace Microsoft.Agents.AI.AGUI.Extensions;

/// <summary>
/// Extensions for converting AG-UI events to ChatResponseUpdate (client-side: events → MEAI).
/// </summary>
internal static class ClientChatResponseUpdateAGUIExtensions
{
    public static async IAsyncEnumerable<ChatResponseUpdate> AsChatResponseUpdatesAsync(
        this IAsyncEnumerable<BaseEvent> events,
        JsonSerializerOptions jsonSerializerOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? conversationId = null;
        string? responseId = null;
        var textMessageBuilder = new TextMessageBuilder();
        var toolCallAccumulator = new ToolCallBuilder();
        await foreach (var evt in events.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            switch (evt)
            {
                // Lifecycle events
                case RunStartedEvent runStarted:
                    conversationId = runStarted.ThreadId;
                    responseId = runStarted.RunId;
                    toolCallAccumulator.SetConversationAndResponseIds(conversationId, responseId);
                    textMessageBuilder.SetConversationAndResponseIds(conversationId, responseId);
                    yield return ValidateAndEmitRunStart(runStarted);
                    break;
                case RunFinishedEvent runFinished:
                    yield return ValidateAndEmitRunFinished(conversationId, responseId, runFinished, jsonSerializerOptions);
                    break;
                case RunErrorEvent runError:
                    yield return new ChatResponseUpdate(ChatRole.Assistant, [(new ErrorContent(runError.Message) { ErrorCode = runError.Code })])
                    {
                        RawRepresentation = runError
                    };
                    break;

                // Text events
                case TextMessageStartEvent textStart:
                    textMessageBuilder.AddTextStart(textStart);
                    break;
                case TextMessageContentEvent textContent:
                    yield return textMessageBuilder.EmitTextUpdate(textContent);
                    break;
                case TextMessageEndEvent textEnd:
                    textMessageBuilder.EndCurrentMessage(textEnd);
                    break;

                // Tool call events
                case ToolCallStartEvent toolCallStart:
                    toolCallAccumulator.AddToolCallStart(toolCallStart);
                    break;
                case ToolCallArgsEvent toolCallArgs:
                    toolCallAccumulator.AddToolCallArgs(toolCallArgs, jsonSerializerOptions);
                    break;
                case ToolCallEndEvent toolCallEnd:
                    yield return toolCallAccumulator.EmitToolCallUpdate(toolCallEnd, jsonSerializerOptions);
                    break;
                case ToolCallResultEvent toolCallResult:
                    yield return toolCallAccumulator.EmitToolCallResult(toolCallResult, jsonSerializerOptions);
                    break;

                // State snapshot events
                case StateSnapshotEvent stateSnapshot:
                    if (stateSnapshot.Snapshot.HasValue)
                    {
                        yield return CreateStateSnapshotUpdate(stateSnapshot, conversationId, responseId, jsonSerializerOptions);
                    }
                    break;
                case StateDeltaEvent stateDelta:
                    if (stateDelta.Delta.HasValue)
                    {
                        yield return CreateStateDeltaUpdate(stateDelta, conversationId, responseId, jsonSerializerOptions);
                    }
                    break;

                // Custom events - emit directly with RawRepresentation
                case CustomEvent customEvent:
                    yield return new ChatResponseUpdate(ChatRole.Assistant, [])
                    {
                        ConversationId = conversationId,
                        ResponseId = responseId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        RawRepresentation = customEvent
                    };
                    break;

                // Reasoning events
                case ReasoningStartEvent reasoningStart:
                    yield return new ChatResponseUpdate(ChatRole.Assistant, [])
                    {
                        ConversationId = conversationId,
                        ResponseId = responseId,
                        MessageId = reasoningStart.MessageId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        RawRepresentation = reasoningStart
                    };
                    break;
                case ReasoningEndEvent reasoningEnd:
                    yield return new ChatResponseUpdate(ChatRole.Assistant, [])
                    {
                        ConversationId = conversationId,
                        ResponseId = responseId,
                        MessageId = reasoningEnd.MessageId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        RawRepresentation = reasoningEnd
                    };
                    break;
                case ReasoningMessageStartEvent reasoningMessageStart:
                    yield return new ChatResponseUpdate(ChatRole.Assistant, [])
                    {
                        ConversationId = conversationId,
                        ResponseId = responseId,
                        MessageId = reasoningMessageStart.MessageId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        RawRepresentation = reasoningMessageStart
                    };
                    break;
                case ReasoningMessageContentEvent reasoningMessageContent:
                    yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent(reasoningMessageContent.Delta)])
                    {
                        ConversationId = conversationId,
                        ResponseId = responseId,
                        MessageId = reasoningMessageContent.MessageId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        RawRepresentation = reasoningMessageContent
                    };
                    break;
                case ReasoningMessageEndEvent reasoningMessageEnd:
                    yield return new ChatResponseUpdate(ChatRole.Assistant, [])
                    {
                        ConversationId = conversationId,
                        ResponseId = responseId,
                        MessageId = reasoningMessageEnd.MessageId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        RawRepresentation = reasoningMessageEnd
                    };
                    break;
                case ReasoningMessageChunkEvent reasoningMessageChunk:
                    if (!string.IsNullOrEmpty(reasoningMessageChunk.Delta))
                    {
                        yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent(reasoningMessageChunk.Delta)])
                        {
                            ConversationId = conversationId,
                            ResponseId = responseId,
                            MessageId = reasoningMessageChunk.MessageId,
                            CreatedAt = DateTimeOffset.UtcNow,
                            RawRepresentation = reasoningMessageChunk
                        };
                    }
                    else
                    {
                        yield return new ChatResponseUpdate(ChatRole.Assistant, [])
                        {
                            ConversationId = conversationId,
                            ResponseId = responseId,
                            MessageId = reasoningMessageChunk.MessageId,
                            CreatedAt = DateTimeOffset.UtcNow,
                            RawRepresentation = reasoningMessageChunk
                        };
                    }
                    break;
            }
        }
    }

    private static ChatResponseUpdate CreateStateSnapshotUpdate(
        StateSnapshotEvent stateSnapshot,
        string? conversationId,
        string? responseId,
        JsonSerializerOptions jsonSerializerOptions)
    {
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(
            stateSnapshot.Snapshot!.Value,
            jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)));
        DataContent dataContent = new(jsonBytes, "application/json");

        return new ChatResponseUpdate(ChatRole.Assistant, [dataContent])
        {
            ConversationId = conversationId,
            ResponseId = responseId,
            CreatedAt = DateTimeOffset.UtcNow,
            RawRepresentation = stateSnapshot,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["is_state_snapshot"] = true
            }
        };
    }

    private static ChatResponseUpdate CreateStateDeltaUpdate(
        StateDeltaEvent stateDelta,
        string? conversationId,
        string? responseId,
        JsonSerializerOptions jsonSerializerOptions)
    {
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(
            stateDelta.Delta!.Value,
            jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)));
        DataContent dataContent = new(jsonBytes, "application/json-patch+json");

        return new ChatResponseUpdate(ChatRole.Assistant, [dataContent])
        {
            ConversationId = conversationId,
            ResponseId = responseId,
            CreatedAt = DateTimeOffset.UtcNow,
            RawRepresentation = stateDelta,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["is_state_delta"] = true
            }
        };
    }

    private sealed class TextMessageBuilder()
    {
        private ChatRole _currentRole;
        private string? _currentMessageId;
        private string? _conversationId;
        private string? _responseId;

        public void SetConversationAndResponseIds(string? conversationId, string? responseId)
        {
            this._conversationId = conversationId;
            this._responseId = responseId;
        }

        public void AddTextStart(TextMessageStartEvent textStart)
        {
            if (this._currentRole != default || this._currentMessageId != null)
            {
                throw new InvalidOperationException("Received TextMessageStartEvent while another message is being processed.");
            }

            this._currentRole = ClientAGUIChatMessageExtensions.MapChatRole(textStart.Role);
            this._currentMessageId = textStart.MessageId;
        }

        internal ChatResponseUpdate EmitTextUpdate(TextMessageContentEvent textContent)
        {
            return new ChatResponseUpdate(
                this._currentRole,
                textContent.Delta)
            {
                ConversationId = this._conversationId,
                ResponseId = this._responseId,
                MessageId = textContent.MessageId,
                CreatedAt = DateTimeOffset.UtcNow,
                RawRepresentation = textContent
            };
        }

        internal void EndCurrentMessage(TextMessageEndEvent textEnd)
        {
            if (this._currentMessageId != textEnd.MessageId)
            {
                throw new InvalidOperationException("Received TextMessageEndEvent for a different message than the current one.");
            }
            this._currentRole = default;
            this._currentMessageId = null;
        }
    }

    private static ChatResponseUpdate ValidateAndEmitRunStart(RunStartedEvent runStarted)
    {
        return new ChatResponseUpdate(
            ChatRole.Assistant,
            [])
        {
            ConversationId = runStarted.ThreadId,
            ResponseId = runStarted.RunId,
            CreatedAt = DateTimeOffset.UtcNow,
            RawRepresentation = runStarted
        };
    }

    private static ChatResponseUpdate ValidateAndEmitRunFinished(string? conversationId, string? responseId, RunFinishedEvent runFinished, JsonSerializerOptions jsonSerializerOptions)
    {
        if (!string.Equals(runFinished.ThreadId, conversationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The run finished event didn't match the run started event thread ID: {runFinished.ThreadId}, {conversationId}");
        }
        if (!string.Equals(runFinished.RunId, responseId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The run finished event didn't match the run started event run ID: {runFinished.RunId}, {responseId}");
        }

        // Check if this is an interrupt
        var isInterrupt = string.Equals(runFinished.Outcome, RunFinishedOutcome.Interrupt, StringComparison.OrdinalIgnoreCase)
            || (runFinished.Interrupt is not null && runFinished.Outcome is null);

        if (isInterrupt && runFinished.Interrupt is { } interrupt)
        {
            var interruptContent = ClientInterruptContentExtensions.FromAGUIInterrupt(interrupt);
            return new ChatResponseUpdate(
                ChatRole.Assistant, [interruptContent])
            {
                ConversationId = conversationId,
                ResponseId = responseId,
                CreatedAt = DateTimeOffset.UtcNow,
                RawRepresentation = runFinished
            };
        }

        return new ChatResponseUpdate(
            ChatRole.Assistant, runFinished.Result?.GetRawText())
        {
            ConversationId = conversationId,
            ResponseId = responseId,
            CreatedAt = DateTimeOffset.UtcNow,
            RawRepresentation = runFinished
        };
    }

    private sealed class ToolCallBuilder
    {
        private string? _conversationId;
        private string? _responseId;
        private StringBuilder? _accumulatedArgs;
        private FunctionCallContent? _currentFunctionCall;

        public void AddToolCallStart(ToolCallStartEvent toolCallStart)
        {
            if (this._currentFunctionCall != null)
            {
                throw new InvalidOperationException("Received ToolCallStartEvent while another tool call is being processed.");
            }
            this._accumulatedArgs ??= new StringBuilder();
            this._currentFunctionCall = new(
                    toolCallStart.ToolCallId,
                    toolCallStart.ToolCallName,
                    null);
        }

        public void AddToolCallArgs(ToolCallArgsEvent toolCallArgs, JsonSerializerOptions options)
        {
            if (this._currentFunctionCall == null)
            {
                throw new InvalidOperationException("Received ToolCallArgsEvent without a current tool call.");
            }

            if (!string.Equals(this._currentFunctionCall.CallId, toolCallArgs.ToolCallId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Received ToolCallArgsEvent for a different tool call than the current one.");
            }

            Debug.Assert(this._accumulatedArgs != null, "Accumulated args should have been initialized in ToolCallStartEvent.");
            this._accumulatedArgs.Append(toolCallArgs.Delta);
        }

        internal ChatResponseUpdate EmitToolCallUpdate(ToolCallEndEvent toolCallEnd, JsonSerializerOptions jsonSerializerOptions)
        {
            if (this._currentFunctionCall == null)
            {
                throw new InvalidOperationException("Received ToolCallEndEvent without a current tool call.");
            }
            if (!string.Equals(this._currentFunctionCall.CallId, toolCallEnd.ToolCallId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Received ToolCallEndEvent for a different tool call than the current one.");
            }
            Debug.Assert(this._accumulatedArgs != null, "Accumulated args should have been initialized in ToolCallStartEvent.");
            var arguments = DeserializeArgumentsIfAvailable(this._accumulatedArgs.ToString(), jsonSerializerOptions);
            this._accumulatedArgs.Clear();
            this._currentFunctionCall.Arguments = arguments;
            var invocation = this._currentFunctionCall;
            this._currentFunctionCall = null;
            return new ChatResponseUpdate(
                ChatRole.Assistant,
                [invocation])
            {
                ConversationId = this._conversationId,
                ResponseId = this._responseId,
                MessageId = invocation.CallId,
                CreatedAt = DateTimeOffset.UtcNow,
                RawRepresentation = toolCallEnd
            };
        }

        public ChatResponseUpdate EmitToolCallResult(ToolCallResultEvent toolCallResult, JsonSerializerOptions options)
        {
            return new ChatResponseUpdate(
                ChatRole.Tool,
                [new FunctionResultContent(
                    toolCallResult.ToolCallId,
                    DeserializeResultIfAvailable(toolCallResult, options))])
            {
                ConversationId = this._conversationId,
                ResponseId = this._responseId,
                MessageId = toolCallResult.MessageId,
                CreatedAt = DateTimeOffset.UtcNow,
                RawRepresentation = toolCallResult
            };
        }

        internal void SetConversationAndResponseIds(string conversationId, string responseId)
        {
            this._conversationId = conversationId;
            this._responseId = responseId;
        }
    }

    private static IDictionary<string, object?>? DeserializeArgumentsIfAvailable(string argsJson, JsonSerializerOptions options)
    {
        if (!string.IsNullOrEmpty(argsJson))
        {
            return (IDictionary<string, object?>?)JsonSerializer.Deserialize(
                argsJson,
                options.GetTypeInfo(typeof(IDictionary<string, object?>)));
        }

        return null;
    }

    private static object? DeserializeResultIfAvailable(ToolCallResultEvent toolCallResult, JsonSerializerOptions options)
    {
        if (!string.IsNullOrEmpty(toolCallResult.Content))
        {
            return JsonSerializer.Deserialize(toolCallResult.Content, options.GetTypeInfo(typeof(JsonElement)));
        }

        return null;
    }
}
