// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace AGUIDojoServer.Subgraphs;

/// <summary>
/// A simple in-memory implementation of <see cref="JsonCheckpointStore"/> for demo purposes.
/// Note: This is not suitable for production as it doesn't persist across restarts.
/// </summary>
internal sealed class InMemoryJsonCheckpointStore : JsonCheckpointStore
{
    private readonly ConcurrentDictionary<(string RunId, string CheckpointId), JsonElement> _checkpoints = new();
    private readonly ConcurrentDictionary<string, HashSet<CheckpointInfo>> _index = new();

    public override ValueTask<CheckpointInfo> CreateCheckpointAsync(string runId, JsonElement value, CheckpointInfo? parent = null)
    {
        var key = new CheckpointInfo(runId, Guid.NewGuid().ToString("N"));
        _checkpoints[(runId, key.CheckpointId)] = value;

        var runIndex = _index.GetOrAdd(runId, _ => []);
        lock (runIndex)
        {
            runIndex.Add(key);
        }

        Console.WriteLine($"[CHECKPOINT DEBUG] Created checkpoint: runId={runId}, checkpointId={key.CheckpointId}");
        return ValueTask.FromResult(key);
    }

    public override ValueTask<JsonElement> RetrieveCheckpointAsync(string runId, CheckpointInfo key)
    {
        if (_checkpoints.TryGetValue((runId, key.CheckpointId), out var checkpoint))
        {
            Console.WriteLine($"[CHECKPOINT DEBUG] Retrieved checkpoint: runId={runId}, checkpointId={key.CheckpointId}");
            return ValueTask.FromResult(checkpoint);
        }

        Console.WriteLine($"[CHECKPOINT DEBUG] Checkpoint not found: runId={runId}, checkpointId={key.CheckpointId}");
        throw new KeyNotFoundException($"Checkpoint '{key.CheckpointId}' not found for runId '{runId}'.");
    }

    public override ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(string runId, CheckpointInfo? withParent = null)
    {
        if (_index.TryGetValue(runId, out var runIndex))
        {
            lock (runIndex)
            {
                // For this simple in-memory store, we return all checkpoints for the run
                // Full parent tracking would require additional data structures
                var result = runIndex.ToList();
                Console.WriteLine($"[CHECKPOINT DEBUG] Retrieved index: runId={runId}, count={result.Count}");
                return ValueTask.FromResult<IEnumerable<CheckpointInfo>>(result);
            }
        }

        Console.WriteLine($"[CHECKPOINT DEBUG] No index for runId={runId}");
        return ValueTask.FromResult<IEnumerable<CheckpointInfo>>([]);
    }
}
