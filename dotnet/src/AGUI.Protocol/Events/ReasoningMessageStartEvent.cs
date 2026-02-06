// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when a reasoning message starts.
/// </summary>
public sealed class ReasoningMessageStartEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.ReasoningMessageStart;

    /// <summary>
    /// Gets or sets the unique identifier for this reasoning message.
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role of the reasoning message.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = AGUIRoles.Assistant;
}
