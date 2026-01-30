// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Represents a resume payload for continuing from an interrupt.
/// </summary>
/// <remarks>
/// When an agent run is interrupted, the client sends a new <see cref="RunAgentInput"/>
/// with a <see cref="AGUIResume"/> payload to continue execution.
/// </remarks>
public sealed class AGUIResume
{
    /// <summary>
    /// Gets or sets the interrupt ID being responded to.
    /// </summary>
    /// <remarks>
    /// This should match the <see cref="AGUIInterrupt.Id"/> from the interrupt event.
    /// </remarks>
    [JsonPropertyName("interruptId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InterruptId { get; set; }

    /// <summary>
    /// Gets or sets the response payload.
    /// </summary>
    /// <remarks>
    /// For function approval responses, this typically contains an "approved" boolean.
    /// For user input responses, this contains the user's input data.
    /// </remarks>
    [JsonPropertyName("payload")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Payload { get; set; }
}
