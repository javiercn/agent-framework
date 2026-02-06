// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when a step in the agent's execution starts.
/// </summary>
public sealed class StepStartedEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.StepStarted;

    /// <summary>
    /// Gets or sets the step identifier.
    /// </summary>
    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the step.
    /// </summary>
    [JsonPropertyName("stepName")]
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent step identifier for nested steps.
    /// </summary>
    [JsonPropertyName("parentStepId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentStepId { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for the step.
    /// </summary>
    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Metadata { get; set; }
}
