// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when a reasoning message ends.
/// </summary>
public sealed class ReasoningMessageEndEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.ReasoningMessageEnd;

    /// <summary>
    /// Gets or sets the message identifier (matches the ID from <see cref="ReasoningMessageStartEvent"/>).
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;
}
