// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted with a complete snapshot of an activity's state.
/// </summary>
public sealed class ActivitySnapshotEvent : BaseEvent
{
    /// <inheritdoc />
    public override string Type => AGUIEventTypes.ActivitySnapshot;

    /// <summary>
    /// Gets or sets the activity identifier.
    /// </summary>
    [JsonPropertyName("activityId")]
    public string ActivityId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the activity.
    /// </summary>
    [JsonPropertyName("activityType")]
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the activity state.
    /// </summary>
    [JsonPropertyName("state")]
    public JsonElement State { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for the activity.
    /// </summary>
    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Metadata { get; set; }
}
