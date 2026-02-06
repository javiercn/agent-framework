// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AGUIDojoServer.AgenticUI;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by ChatClientAgentFactory.CreateAgenticUI")]
internal sealed class AgenticUIAgent : DelegatingAIAgent
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public AgenticUIAgent(AIAgent innerAgent, JsonSerializerOptions jsonSerializerOptions)
        : base(innerAgent)
    {
        this._jsonSerializerOptions = jsonSerializerOptions;
    }

    protected override Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this.RunCoreStreamingAsync(messages, session, options, cancellationToken).ToAgentResponseAsync(cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Track function calls that should trigger state events
        var trackedFunctionCalls = new Dictionary<string, FunctionCallContent>();

        await foreach (var update in this.InnerAgent.RunStreamingAsync(messages, session, options, cancellationToken).ConfigureAwait(false))
        {
            // Process contents: track function calls and emit state events for results
            List<BaseEvent> stateEventsToEmit = [];
            foreach (var content in update.Contents)
            {
                if (content is FunctionCallContent callContent)
                {
                    if (callContent.Name == "create_plan" || callContent.Name == "update_plan_step")
                    {
                        trackedFunctionCalls[callContent.CallId] = callContent;
                        break;
                    }
                }
                else if (content is FunctionResultContent resultContent)
                {
                    // Check if this result matches a tracked function call
                    if (trackedFunctionCalls.TryGetValue(resultContent.CallId, out var matchedCall))
                    {
                        var jsonResult = (JsonElement)resultContent.Result!;

                        // Determine event type based on the function name
                        if (matchedCall.Name == "create_plan")
                        {
                            // STATE_SNAPSHOT event
                            stateEventsToEmit.Add(new StateSnapshotEvent { Snapshot = jsonResult });
                        }
                        else if (matchedCall.Name == "update_plan_step")
                        {
                            // STATE_DELTA event (JSON Patch)
                            stateEventsToEmit.Add(new StateDeltaEvent { Delta = jsonResult });
                        }
                    }
                }
            }

            yield return update;

            // Emit state events via RawRepresentation
            foreach (var stateEvent in stateEventsToEmit)
            {
                yield return new AgentResponseUpdate
                {
                    AgentId = update.AgentId,
                    CreatedAt = update.CreatedAt,
                    ResponseId = update.ResponseId,
                    Contents = [],
                    RawRepresentation = stateEvent
                };
            }
        }
    }
}
