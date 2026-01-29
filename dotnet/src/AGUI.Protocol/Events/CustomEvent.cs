// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted with a custom application-defined event.
/// </summary>
public sealed class CustomEvent : BaseEvent
{
    /// <inheritdoc />
    public override string Type => AGUIEventTypes.Custom;

    /// <summary>
    /// Gets or sets the name of the custom event.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value of the custom event.
    /// </summary>
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }
}
