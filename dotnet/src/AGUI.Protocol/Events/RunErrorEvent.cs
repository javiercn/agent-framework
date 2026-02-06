// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when an agent run has encountered an error.
/// </summary>
public sealed class RunErrorEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.RunError;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }
}
