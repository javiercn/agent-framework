// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Base class for all AG-UI events.
/// </summary>
[JsonConverter(typeof(BaseEventJsonConverter))]
public abstract class BaseEvent
{
    /// <summary>
    /// Gets the event type identifier.
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}
