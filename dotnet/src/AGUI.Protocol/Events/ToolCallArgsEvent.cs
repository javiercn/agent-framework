// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted with tool call argument chunks.
/// </summary>
public sealed class ToolCallArgsEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.ToolCallArgs;

    /// <summary>
    /// Gets or sets the tool call identifier.
    /// </summary>
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the arguments delta.
    /// </summary>
    [JsonPropertyName("delta")]
    public string Delta { get; set; } = string.Empty;
}
