// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Represents a tool result message in the AG-UI protocol.
/// </summary>
public sealed class AGUIToolMessage : AGUIMessage
{
    /// <inheritdoc />
    public override string Role => AGUIRoles.Tool;

    /// <summary>
    /// Gets or sets the tool call identifier this message is responding to.
    /// </summary>
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message if the tool call failed.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}
