// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Represents a tool call made by the assistant.
/// </summary>
public sealed class AGUIToolCall
{
    /// <summary>
    /// Gets or sets the identifier of the tool call.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the tool call.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    /// Gets or sets the function call details.
    /// </summary>
    [JsonPropertyName("function")]
    public AGUIFunctionCall Function { get; set; } = new();
}
