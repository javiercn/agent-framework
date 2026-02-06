// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted with reasoning message content chunks.
/// </summary>
public sealed class ReasoningMessageContentEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.ReasoningMessageContent;

    /// <summary>
    /// Gets or sets the message identifier (matches the ID from <see cref="ReasoningMessageStartEvent"/>).
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reasoning content chunk (non-empty).
    /// </summary>
    [JsonPropertyName("delta")]
    public string Delta { get; set; } = string.Empty;
}
