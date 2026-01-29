// Copyright (c) Microsoft. All rights reserved.

namespace AGUI.Protocol;

/// <summary>
/// Event emitted when the agent exits a thinking phase.
/// </summary>
public sealed class ThinkingEndEvent : BaseEvent
{
    /// <inheritdoc />
    public override string Type => AGUIEventTypes.ThinkingEnd;
}
