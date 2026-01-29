// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AGUI.Protocol;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CounterAssistant;

/// <summary>
/// A simple agent that demonstrates emitting AG-UI events directly via RawRepresentation.
/// This agent maintains a counter state and emits StateSnapshotEvent directly
/// instead of using DataContent with application/json media type.
/// </summary>
public sealed class CounterAgent : DelegatingAIAgent
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public CounterAgent(IChatClient chatClient)
        : base(chatClient.AsAIAgent("counter-inner", "Inner chat client agent"))
    {
        this._jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = CounterSerializerContext.Default
        };
    }

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return this.RunCoreStreamingAsync(messages, session, options, cancellationToken)
            .ToAgentResponseAsync(cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Extract state from AG-UI request
        CounterState currentState = GetStateFromOptions(options);

        // Check the user message for counter commands
        string? userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text;

        bool shouldModifyState = false;
        if (!string.IsNullOrEmpty(userMessage))
        {
            string upperMessage = userMessage.ToUpperInvariant();
            if (upperMessage.Contains("INCREMENT", StringComparison.Ordinal) ||
                upperMessage.Contains("ADD", StringComparison.Ordinal) ||
                upperMessage.Contains('+'))
            {
                currentState.Counter++;
                currentState.LastAction = "incremented";
                shouldModifyState = true;
            }
            else if (upperMessage.Contains("DECREMENT", StringComparison.Ordinal) ||
                upperMessage.Contains("SUBTRACT", StringComparison.Ordinal) ||
                upperMessage.Contains('-'))
            {
                currentState.Counter--;
                currentState.LastAction = "decremented";
                shouldModifyState = true;
            }
            else if (upperMessage.Contains("RESET", StringComparison.Ordinal) ||
                upperMessage.Contains("ZERO", StringComparison.Ordinal))
            {
                currentState.Counter = 0;
                currentState.LastAction = "reset";
                shouldModifyState = true;
            }
        }

        // If we modified the state, emit a StateSnapshotEvent directly
        // This demonstrates the new pattern of emitting AG-UI events via RawRepresentation
        if (shouldModifyState)
        {
            // Serialize the state to a JsonElement
            JsonElement stateJson = JsonSerializer.SerializeToElement(currentState, this._jsonSerializerOptions);

            // Create a StateSnapshotEvent and emit it via RawRepresentation
            var stateSnapshotEvent = new StateSnapshotEvent
            {
                Snapshot = stateJson
            };

            // Yield an AgentResponseUpdate with the raw AG-UI event
            // When this flows through AsAGUIEventStreamAsync, it will detect the
            // RawRepresentation contains a BaseEvent and emit it directly
            yield return new AgentResponseUpdate
            {
                RawRepresentation = stateSnapshotEvent,
                // Contents can be empty since the event carries the data
                Contents = []
            };
        }

        // Now stream a text response from the inner agent
        await foreach (var update in this.InnerAgent.RunStreamingAsync(messages, session, options, cancellationToken))
        {
            yield return update;
        }
    }

    private static CounterState GetStateFromOptions(AgentRunOptions? options)
    {
        if (options is ChatClientAgentRunOptions { ChatOptions.AdditionalProperties: { } properties } &&
            properties.TryGetValue("ag_ui_state", out object? stateObj) &&
            stateObj is JsonElement state &&
            state.ValueKind == JsonValueKind.Object &&
            state.TryGetProperty("counter", out JsonElement counterElement) &&
            counterElement.ValueKind == JsonValueKind.Number)
        {
            return new CounterState
            {
                Counter = counterElement.GetInt32(),
                LastAction = state.TryGetProperty("lastAction", out JsonElement actionElement)
                    ? actionElement.GetString() ?? "none"
                    : "none"
            };
        }

        return new CounterState();
    }
}

internal sealed class CounterState
{
    [JsonPropertyName("counter")]
    public int Counter { get; set; }

    [JsonPropertyName("lastAction")]
    public string LastAction { get; set; } = "none";
}

[JsonSerializable(typeof(CounterState))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class CounterSerializerContext : JsonSerializerContext;
