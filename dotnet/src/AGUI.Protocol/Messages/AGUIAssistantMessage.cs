// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Represents an assistant message in the AG-UI protocol.
/// </summary>
public sealed class AGUIAssistantMessage : AGUIMessage
{
    /// <inheritdoc />
    public override string Role => AGUIRoles.Assistant;

    /// <summary>
    /// Gets or sets the name of the assistant.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the tool calls made by the assistant.
    /// </summary>
    [JsonPropertyName("toolCalls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AGUIToolCall>? ToolCalls { get; set; }
}
