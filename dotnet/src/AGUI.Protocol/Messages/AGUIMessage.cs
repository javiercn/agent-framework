// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Base class for all AG-UI messages.
/// </summary>
[JsonConverter(typeof(AGUIMessageJsonConverter))]
public abstract class AGUIMessage
{
    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>
    /// Gets the role of the message sender.
    /// </summary>
    [JsonPropertyName("role")]
    public abstract string Role { get; }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
