// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when a thinking text message ends.
/// </summary>
public sealed class ThinkingTextMessageEndEvent : BaseEvent
{
    /// <inheritdoc />
    public override string Type => AGUIEventTypes.ThinkingTextMessageEnd;

    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;
}
