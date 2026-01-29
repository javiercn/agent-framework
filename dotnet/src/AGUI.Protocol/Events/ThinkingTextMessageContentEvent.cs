// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted with thinking text message content chunks.
/// </summary>
public sealed class ThinkingTextMessageContentEvent : BaseEvent
{
    /// <inheritdoc />
    public override string Type => AGUIEventTypes.ThinkingTextMessageContent;

    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content delta.
    /// </summary>
    [JsonPropertyName("delta")]
    public string Delta { get; set; } = string.Empty;
}
