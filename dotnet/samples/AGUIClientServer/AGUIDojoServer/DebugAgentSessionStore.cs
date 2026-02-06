// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;

namespace AGUIDojoServer;

/// <summary>
/// A debugging wrapper around InMemoryAgentSessionStore that logs all operations.
/// </summary>
internal sealed class DebugAgentSessionStore : AgentSessionStore
{
    private readonly InMemoryAgentSessionStore _inner = new();

    public override async ValueTask SaveSessionAsync(AIAgent agent, string conversationId, AgentSession session, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[SESSION DEBUG] SaveSessionAsync called for conversationId=" + conversationId);

        // Get the serialized session to inspect it
        var serialized = session.Serialize();
        var json = serialized.GetRawText();

        // Truncate for logging
        var truncated = json.Length > 500 ? string.Concat(json.AsSpan(0, 500), "...") : json;
        Console.WriteLine("[SESSION DEBUG] Serialized session: " + truncated);

        // Check if lastCheckpoint is in the serialized data
        if (serialized.TryGetProperty("lastCheckpoint", out var checkpoint) ||
            serialized.TryGetProperty("LastCheckpoint", out checkpoint))
        {
            Console.WriteLine("[SESSION DEBUG] Session HAS LastCheckpoint: " + checkpoint.GetRawText());
        }
        else
        {
            Console.WriteLine("[SESSION DEBUG] Session does NOT have LastCheckpoint in serialized data");

            // List all top-level properties
            if (serialized.ValueKind == JsonValueKind.Object)
            {
                Console.WriteLine("[SESSION DEBUG] Top-level properties: " + string.Join(", ", serialized.EnumerateObject().Select(p => p.Name)));
            }
        }

        await _inner.SaveSessionAsync(agent, conversationId, session, cancellationToken);
        Console.WriteLine("[SESSION DEBUG] Session saved successfully");
    }

    public override async ValueTask<AgentSession> GetSessionAsync(AIAgent agent, string conversationId, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[SESSION DEBUG] GetSessionAsync called for conversationId=" + conversationId + ", agentId=" + agent.Id);

        var session = await _inner.GetSessionAsync(agent, conversationId, cancellationToken);

        Console.WriteLine("[SESSION DEBUG] Session type: " + session.GetType().Name);

        // Try to get LastCheckpoint if it's a WorkflowSession
        var lastCheckpointProp = session.GetType().GetProperty("LastCheckpoint");
        if (lastCheckpointProp != null)
        {
            var lastCheckpoint = lastCheckpointProp.GetValue(session);
            if (lastCheckpoint != null)
            {
                Console.WriteLine("[SESSION DEBUG] Session HAS LastCheckpoint: " + lastCheckpoint);
            }
            else
            {
                Console.WriteLine("[SESSION DEBUG] Session LastCheckpoint is NULL");
            }
        }

        return session;
    }
}
