// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// JSON serializer context for AG-UI protocol types.
/// </summary>
/// <remarks>
/// This context provides source-generated serialization support for all AG-UI types,
/// enabling AOT compilation and improved performance.
/// </remarks>
[JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
// Input types
[JsonSerializable(typeof(RunAgentInput))]
// Message types
[JsonSerializable(typeof(AGUIMessage))]
[JsonSerializable(typeof(AGUIMessage[]))]
[JsonSerializable(typeof(AGUIDeveloperMessage))]
[JsonSerializable(typeof(AGUISystemMessage))]
[JsonSerializable(typeof(AGUIUserMessage))]
[JsonSerializable(typeof(AGUIAssistantMessage))]
[JsonSerializable(typeof(AGUIToolMessage))]
// Multimodal input content types
[JsonSerializable(typeof(AGUIInputContent))]
[JsonSerializable(typeof(AGUIInputContent[]))]
[JsonSerializable(typeof(List<AGUIInputContent>))]
[JsonSerializable(typeof(IList<AGUIInputContent>))]
[JsonSerializable(typeof(AGUITextInputContent))]
[JsonSerializable(typeof(AGUIBinaryInputContent))]
// Tool types
[JsonSerializable(typeof(AGUITool))]
[JsonSerializable(typeof(AGUIToolCall))]
[JsonSerializable(typeof(AGUIToolCall[]))]
[JsonSerializable(typeof(AGUIFunctionCall))]
// Context types
[JsonSerializable(typeof(AGUIContextItem))]
[JsonSerializable(typeof(AGUIContextItem[]))]
// Base event types
[JsonSerializable(typeof(BaseEvent))]
[JsonSerializable(typeof(BaseEvent[]))]
// Existing event types
[JsonSerializable(typeof(RunStartedEvent))]
[JsonSerializable(typeof(RunFinishedEvent))]
[JsonSerializable(typeof(RunErrorEvent))]
[JsonSerializable(typeof(TextMessageStartEvent))]
[JsonSerializable(typeof(TextMessageContentEvent))]
[JsonSerializable(typeof(TextMessageEndEvent))]
[JsonSerializable(typeof(ToolCallStartEvent))]
[JsonSerializable(typeof(ToolCallArgsEvent))]
[JsonSerializable(typeof(ToolCallEndEvent))]
[JsonSerializable(typeof(ToolCallResultEvent))]
[JsonSerializable(typeof(StateSnapshotEvent))]
[JsonSerializable(typeof(StateDeltaEvent))]
// New event types
[JsonSerializable(typeof(StepStartedEvent))]
[JsonSerializable(typeof(StepFinishedEvent))]
[JsonSerializable(typeof(ActivitySnapshotEvent))]
[JsonSerializable(typeof(ActivityDeltaEvent))]
[JsonSerializable(typeof(MessagesSnapshotEvent))]
[JsonSerializable(typeof(RawEvent))]
[JsonSerializable(typeof(CustomEvent))]
// Reasoning event types
[JsonSerializable(typeof(ReasoningStartEvent))]
[JsonSerializable(typeof(ReasoningEndEvent))]
[JsonSerializable(typeof(ReasoningMessageStartEvent))]
[JsonSerializable(typeof(ReasoningMessageContentEvent))]
[JsonSerializable(typeof(ReasoningMessageEndEvent))]
[JsonSerializable(typeof(ReasoningMessageChunkEvent))]
// Primitive and collection types for arbitrary data
[JsonSerializable(typeof(IDictionary<string, object?>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(IDictionary<string, JsonElement?>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement?>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(decimal))]
public sealed partial class AGUIJsonSerializerContext : JsonSerializerContext;
