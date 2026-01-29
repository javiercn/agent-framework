// Copyright (c) Microsoft. All rights reserved.

namespace AGUI.Protocol;

/// <summary>
/// Constants for AG-UI event type identifiers.
/// </summary>
public static class AGUIEventTypes
{
    /// <summary>
    /// Indicates that an agent run has started.
    /// </summary>
    public const string RunStarted = "RUN_STARTED";

    /// <summary>
    /// Indicates that an agent run has finished successfully.
    /// </summary>
    public const string RunFinished = "RUN_FINISHED";

    /// <summary>
    /// Indicates that an agent run has encountered an error.
    /// </summary>
    public const string RunError = "RUN_ERROR";

    /// <summary>
    /// Indicates the start of a text message from the agent.
    /// </summary>
    public const string TextMessageStart = "TEXT_MESSAGE_START";

    /// <summary>
    /// Contains a chunk of text content for an ongoing message.
    /// </summary>
    public const string TextMessageContent = "TEXT_MESSAGE_CONTENT";

    /// <summary>
    /// Indicates the end of a text message from the agent.
    /// </summary>
    public const string TextMessageEnd = "TEXT_MESSAGE_END";

    /// <summary>
    /// Indicates the start of a tool call.
    /// </summary>
    public const string ToolCallStart = "TOOL_CALL_START";

    /// <summary>
    /// Contains arguments for an ongoing tool call.
    /// </summary>
    public const string ToolCallArgs = "TOOL_CALL_ARGS";

    /// <summary>
    /// Indicates the end of a tool call.
    /// </summary>
    public const string ToolCallEnd = "TOOL_CALL_END";

    /// <summary>
    /// Contains the result of a tool call.
    /// </summary>
    public const string ToolCallResult = "TOOL_CALL_RESULT";

    /// <summary>
    /// Contains a complete snapshot of the agent's state.
    /// </summary>
    public const string StateSnapshot = "STATE_SNAPSHOT";

    /// <summary>
    /// Contains a delta update to the agent's state.
    /// </summary>
    public const string StateDelta = "STATE_DELTA";

    /// <summary>
    /// Indicates the start of a step in the agent's execution.
    /// </summary>
    public const string StepStarted = "STEP_STARTED";

    /// <summary>
    /// Indicates the end of a step in the agent's execution.
    /// </summary>
    public const string StepFinished = "STEP_FINISHED";

    /// <summary>
    /// Contains a complete snapshot of an activity's state.
    /// </summary>
    public const string ActivitySnapshot = "ACTIVITY_SNAPSHOT";

    /// <summary>
    /// Contains a delta update to an activity's state.
    /// </summary>
    public const string ActivityDelta = "ACTIVITY_DELTA";

    /// <summary>
    /// Contains a snapshot of the conversation messages.
    /// </summary>
    public const string MessagesSnapshot = "MESSAGES_SNAPSHOT";

    /// <summary>
    /// Indicates the start of a thinking phase.
    /// </summary>
    public const string ThinkingStart = "THINKING_START";

    /// <summary>
    /// Indicates the end of a thinking phase.
    /// </summary>
    public const string ThinkingEnd = "THINKING_END";

    /// <summary>
    /// Indicates the start of a thinking text message.
    /// </summary>
    public const string ThinkingTextMessageStart = "THINKING_TEXT_MESSAGE_START";

    /// <summary>
    /// Contains a chunk of thinking text content.
    /// </summary>
    public const string ThinkingTextMessageContent = "THINKING_TEXT_MESSAGE_CONTENT";

    /// <summary>
    /// Indicates the end of a thinking text message.
    /// </summary>
    public const string ThinkingTextMessageEnd = "THINKING_TEXT_MESSAGE_END";

    /// <summary>
    /// Contains a raw event from an underlying provider.
    /// </summary>
    public const string Raw = "RAW";

    /// <summary>
    /// Contains a custom application-defined event.
    /// </summary>
    public const string Custom = "CUSTOM";
}
