// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when an agent run has finished successfully or been interrupted.
/// </summary>
public sealed class RunFinishedEvent : BaseEvent
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public override string Type => AGUIEventTypes.RunFinished;

    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the run identifier.
    /// </summary>
    [JsonPropertyName("runId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RunId { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the run.
    /// </summary>
    /// <remarks>
    /// When "success", the <see cref="Result"/> property contains the run result.
    /// When "interrupt", the <see cref="Interrupt"/> property contains the interrupt payload.
    /// This property is optional for backward compatibility - if absent, presence of
    /// <see cref="Result"/> implies success, presence of <see cref="Interrupt"/> implies interrupt.
    /// </remarks>
    [JsonPropertyName("outcome")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Outcome { get; set; }

    /// <summary>
    /// Gets or sets the result of the run.
    /// </summary>
    /// <remarks>
    /// Present when <see cref="Outcome"/> is "success" or absent (backward compatibility).
    /// </remarks>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; set; }

    /// <summary>
    /// Gets or sets the interrupt payload when the run is interrupted.
    /// </summary>
    /// <remarks>
    /// Present when <see cref="Outcome"/> is "interrupt".
    /// Contains the interrupt ID and payload data needed to resume the run.
    /// </remarks>
    [JsonPropertyName("interrupt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AGUIInterrupt? Interrupt { get; set; }
}
