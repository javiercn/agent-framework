// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted with a delta update to an activity's state.
/// </summary>
public sealed class ActivityDeltaEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.ActivityDelta;

    /// <summary>
    /// Gets or sets the activity identifier.
    /// </summary>
    [JsonPropertyName("activityId")]
    public string ActivityId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the activity delta.
    /// </summary>
    [JsonPropertyName("delta")]
    public JsonElement Delta { get; set; }
}
