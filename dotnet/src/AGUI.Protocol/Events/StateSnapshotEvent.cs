// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted with a complete snapshot of the agent's state.
/// </summary>
public sealed class StateSnapshotEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.StateSnapshot;

    /// <summary>
    /// Gets or sets the state snapshot.
    /// </summary>
    [JsonPropertyName("snapshot")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Snapshot { get; set; }
}
