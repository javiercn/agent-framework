// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when a tool call ends.
/// </summary>
public sealed class ToolCallEndEvent : BaseEvent
{
    /// <inheritdoc />
    public override string Type => AGUIEventTypes.ToolCallEnd;

    /// <summary>
    /// Gets or sets the tool call identifier.
    /// </summary>
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; set; } = string.Empty;
}
