// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted with a delta update to the agent's state.
/// </summary>
public sealed class StateDeltaEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.StateDelta;

    /// <summary>
    /// Gets or sets the state delta.
    /// </summary>
    [JsonPropertyName("delta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Delta { get; set; }
}
