// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when the agent starts reasoning.
/// </summary>
public sealed class ReasoningStartEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.ReasoningStart;

    /// <summary>
    /// Gets or sets the unique identifier for this reasoning session.
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional encrypted content for privacy-preserving reasoning.
    /// </summary>
    [JsonPropertyName("encryptedContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EncryptedContent { get; set; }
}
