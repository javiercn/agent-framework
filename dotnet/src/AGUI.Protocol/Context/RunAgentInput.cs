// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Represents the input to run an AG-UI agent.
/// </summary>
public sealed class RunAgentInput
{
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
    /// This enables branching conversations where a new run can be created from any point in history.
    /// </remarks>
    [JsonPropertyName("parentRunId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentRunId { get; set; }

    /// <summary>
    /// Gets or sets the agent state.
    /// </summary>
    [JsonPropertyName("state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement State { get; set; }

    /// <summary>
    /// Gets or sets the conversation messages.
    /// </summary>
    [JsonPropertyName("messages")]
    public IEnumerable<AGUIMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the tools available to the agent.
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IEnumerable<AGUITool>? Tools { get; set; }

    /// <summary>
    /// Gets or sets the context items provided to the agent.
    /// </summary>
    [JsonPropertyName("context")]
    public IReadOnlyList<AGUIContextItem> Context { get; set; } = [];

    /// <summary>
    /// Gets or sets additional forwarded properties.
    /// </summary>
    [JsonPropertyName("forwardedProps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement ForwardedProperties { get; set; }

    /// <summary>
    /// Gets or sets the resume payload for continuing from an interrupt.
    /// </summary>
    /// <remarks>
    /// When resuming from an interrupt, this contains the user's response
    /// to the interrupt request (approval decision or input data).
    /// </remarks>
    [JsonPropertyName("resume")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AGUIResume? Resume { get; set; }
}
