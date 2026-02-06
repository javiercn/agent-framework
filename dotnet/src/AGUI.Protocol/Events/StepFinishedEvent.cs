// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when a step in the agent's execution finishes.
/// </summary>
public sealed class StepFinishedEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.StepFinished;

    /// <summary>
    /// Gets or sets the step identifier.
    /// </summary>
    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable name of the step.
    /// </summary>
    [JsonPropertyName("stepName")]
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status of the step (e.g., "completed", "failed", "skipped").
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the result of the step.
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; set; }
}
