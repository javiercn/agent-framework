// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted with a snapshot of the conversation messages.
/// </summary>
public sealed class MessagesSnapshotEvent : BaseEvent
{
    /// <inheritdoc />
    public override string Type => AGUIEventTypes.MessagesSnapshot;

    /// <summary>
    /// Gets or sets the list of messages in the conversation.
    /// </summary>
    [JsonPropertyName("messages")]
    public IReadOnlyList<AGUIMessage> Messages { get; set; } = [];
}
