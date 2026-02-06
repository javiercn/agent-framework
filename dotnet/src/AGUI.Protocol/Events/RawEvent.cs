// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted with a raw event from an underlying provider.
/// </summary>
public sealed class RawEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.Raw;

    /// <summary>
    /// Gets or sets the source of the raw event.
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw event data.
    /// </summary>
    [JsonPropertyName("event")]
    public JsonElement Event { get; set; }
}
