// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when a thinking text message starts.
/// </summary>
public sealed class ThinkingTextMessageStartEvent : BaseEvent
{
    /// <inheritdoc />
    public override string Type => AGUIEventTypes.ThinkingTextMessageStart;

    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;
}
