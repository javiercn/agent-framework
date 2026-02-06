// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Represents an interrupt payload in an AG-UI run finished event.
/// </summary>
/// <remarks>
/// Interrupts enable human-in-the-loop patterns where an agent pauses execution
/// to request approval or gather additional input from the user.
/// </remarks>
public sealed class AGUIInterrupt
{
    /// <summary>
    /// Gets or sets the unique identifier for this interrupt.
    /// </summary>
    /// <remarks>
    /// This ID should be echoed back in the resume payload to identify which interrupt is being responded to.
    /// For function approval requests, this is typically the function call ID.
    /// </remarks>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the payload data for the interrupt.
    /// </summary>
    /// <remarks>
    /// For function approval requests, this contains the function name and arguments.
    /// For user input requests, this contains custom data describing what input is needed.
    /// </remarks>
    [JsonPropertyName("payload")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Payload { get; set; }
}
