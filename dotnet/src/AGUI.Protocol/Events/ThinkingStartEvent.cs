// Copyright (c) Microsoft. All rights reserved.

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when the agent enters a thinking phase.
/// </summary>
public sealed class ThinkingStartEvent : BaseEvent
{
    /// <inheritdoc />
    public override string Type => AGUIEventTypes.ThinkingStart;
}
