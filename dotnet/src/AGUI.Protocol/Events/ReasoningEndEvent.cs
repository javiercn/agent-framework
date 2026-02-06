// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when the agent ends reasoning.
/// </summary>
public sealed class ReasoningEndEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.ReasoningEnd;

    /// <summary>
    /// Gets or sets the unique identifier for this reasoning session.
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;
}
