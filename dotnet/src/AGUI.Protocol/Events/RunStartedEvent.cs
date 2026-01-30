// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when an agent run has started.
/// </summary>
public sealed class RunStartedEvent : BaseEvent
{
    /// <inheritdoc />
    public override string Type => AGUIEventTypes.RunStarted;

    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the run identifier.
    /// </summary>
    [JsonPropertyName("runId")]
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent run identifier for branching/time travel.
    /// </summary>
    /// <remarks>
    /// If present, refers to a prior run within the same thread, creating a git-like append-only log.
    /// </remarks>
    [JsonPropertyName("parentRunId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentRunId { get; set; }

    /// <summary>
    /// Gets or sets the exact agent input payload for this run.
    /// </summary>
    /// <remarks>
    /// May omit messages already present in history; compactEvents() will normalize.
    /// </remarks>
    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunAgentInput? Input { get; set; }
}
