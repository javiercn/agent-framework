// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// JSON converter for polymorphic <see cref="BaseEvent"/> deserialization.
/// </summary>
public sealed class BaseEventJsonConverter : JsonConverter<BaseEvent>
{
    private const string TypeDiscriminatorPropertyName = "type";

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeof(BaseEvent).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override BaseEvent Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var jsonElementTypeInfo = options.GetTypeInfo(typeof(JsonElement));
        JsonElement jsonElement = (JsonElement)JsonSerializer.Deserialize(ref reader, jsonElementTypeInfo)!;

        // Try to get the discriminator property
        if (!jsonElement.TryGetProperty(TypeDiscriminatorPropertyName, out JsonElement discriminatorElement))
        {
            throw new JsonException($"Missing required property '{TypeDiscriminatorPropertyName}' for BaseEvent deserialization");
        }

        string? discriminator = discriminatorElement.GetString();

        // Map discriminator to concrete type and deserialize using type info from options
        BaseEvent? result = discriminator switch
        {
            AGUIEventTypes.RunStarted => jsonElement.Deserialize(options.GetTypeInfo(typeof(RunStartedEvent))) as RunStartedEvent,
            AGUIEventTypes.RunFinished => jsonElement.Deserialize(options.GetTypeInfo(typeof(RunFinishedEvent))) as RunFinishedEvent,
            AGUIEventTypes.RunError => jsonElement.Deserialize(options.GetTypeInfo(typeof(RunErrorEvent))) as RunErrorEvent,
            AGUIEventTypes.TextMessageStart => jsonElement.Deserialize(options.GetTypeInfo(typeof(TextMessageStartEvent))) as TextMessageStartEvent,
            AGUIEventTypes.TextMessageContent => jsonElement.Deserialize(options.GetTypeInfo(typeof(TextMessageContentEvent))) as TextMessageContentEvent,
            AGUIEventTypes.TextMessageEnd => jsonElement.Deserialize(options.GetTypeInfo(typeof(TextMessageEndEvent))) as TextMessageEndEvent,
            AGUIEventTypes.ToolCallStart => jsonElement.Deserialize(options.GetTypeInfo(typeof(ToolCallStartEvent))) as ToolCallStartEvent,
            AGUIEventTypes.ToolCallArgs => jsonElement.Deserialize(options.GetTypeInfo(typeof(ToolCallArgsEvent))) as ToolCallArgsEvent,
            AGUIEventTypes.ToolCallEnd => jsonElement.Deserialize(options.GetTypeInfo(typeof(ToolCallEndEvent))) as ToolCallEndEvent,
            AGUIEventTypes.ToolCallResult => jsonElement.Deserialize(options.GetTypeInfo(typeof(ToolCallResultEvent))) as ToolCallResultEvent,
            AGUIEventTypes.StateSnapshot => jsonElement.Deserialize(options.GetTypeInfo(typeof(StateSnapshotEvent))) as StateSnapshotEvent,
            AGUIEventTypes.StateDelta => jsonElement.Deserialize(options.GetTypeInfo(typeof(StateDeltaEvent))) as StateDeltaEvent,
            AGUIEventTypes.StepStarted => jsonElement.Deserialize(options.GetTypeInfo(typeof(StepStartedEvent))) as StepStartedEvent,
            AGUIEventTypes.StepFinished => jsonElement.Deserialize(options.GetTypeInfo(typeof(StepFinishedEvent))) as StepFinishedEvent,
            AGUIEventTypes.ActivitySnapshot => jsonElement.Deserialize(options.GetTypeInfo(typeof(ActivitySnapshotEvent))) as ActivitySnapshotEvent,
            AGUIEventTypes.ActivityDelta => jsonElement.Deserialize(options.GetTypeInfo(typeof(ActivityDeltaEvent))) as ActivityDeltaEvent,
            AGUIEventTypes.MessagesSnapshot => jsonElement.Deserialize(options.GetTypeInfo(typeof(MessagesSnapshotEvent))) as MessagesSnapshotEvent,
            AGUIEventTypes.ThinkingStart => jsonElement.Deserialize(options.GetTypeInfo(typeof(ThinkingStartEvent))) as ThinkingStartEvent,
            AGUIEventTypes.ThinkingEnd => jsonElement.Deserialize(options.GetTypeInfo(typeof(ThinkingEndEvent))) as ThinkingEndEvent,
            AGUIEventTypes.ThinkingTextMessageStart => jsonElement.Deserialize(options.GetTypeInfo(typeof(ThinkingTextMessageStartEvent))) as ThinkingTextMessageStartEvent,
            AGUIEventTypes.ThinkingTextMessageContent => jsonElement.Deserialize(options.GetTypeInfo(typeof(ThinkingTextMessageContentEvent))) as ThinkingTextMessageContentEvent,
            AGUIEventTypes.ThinkingTextMessageEnd => jsonElement.Deserialize(options.GetTypeInfo(typeof(ThinkingTextMessageEndEvent))) as ThinkingTextMessageEndEvent,
            AGUIEventTypes.Raw => jsonElement.Deserialize(options.GetTypeInfo(typeof(RawEvent))) as RawEvent,
            AGUIEventTypes.Custom => jsonElement.Deserialize(options.GetTypeInfo(typeof(CustomEvent))) as CustomEvent,
            _ => throw new JsonException($"Unknown BaseEvent type discriminator: '{discriminator}'")
        };

        if (result == null)
        {
            throw new JsonException($"Failed to deserialize BaseEvent with type discriminator: '{discriminator}'");
        }

        return result;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        BaseEvent value,
        JsonSerializerOptions options)
    {
        // Serialize the concrete type directly using type info from options
        switch (value)
        {
            case RunStartedEvent runStarted:
                JsonSerializer.Serialize(writer, runStarted, options.GetTypeInfo(typeof(RunStartedEvent)));
                break;
            case RunFinishedEvent runFinished:
                JsonSerializer.Serialize(writer, runFinished, options.GetTypeInfo(typeof(RunFinishedEvent)));
                break;
            case RunErrorEvent runError:
                JsonSerializer.Serialize(writer, runError, options.GetTypeInfo(typeof(RunErrorEvent)));
                break;
            case TextMessageStartEvent textStart:
                JsonSerializer.Serialize(writer, textStart, options.GetTypeInfo(typeof(TextMessageStartEvent)));
                break;
            case TextMessageContentEvent textContent:
                JsonSerializer.Serialize(writer, textContent, options.GetTypeInfo(typeof(TextMessageContentEvent)));
                break;
            case TextMessageEndEvent textEnd:
                JsonSerializer.Serialize(writer, textEnd, options.GetTypeInfo(typeof(TextMessageEndEvent)));
                break;
            case ToolCallStartEvent toolCallStart:
                JsonSerializer.Serialize(writer, toolCallStart, options.GetTypeInfo(typeof(ToolCallStartEvent)));
                break;
            case ToolCallArgsEvent toolCallArgs:
                JsonSerializer.Serialize(writer, toolCallArgs, options.GetTypeInfo(typeof(ToolCallArgsEvent)));
                break;
            case ToolCallEndEvent toolCallEnd:
                JsonSerializer.Serialize(writer, toolCallEnd, options.GetTypeInfo(typeof(ToolCallEndEvent)));
                break;
            case ToolCallResultEvent toolCallResult:
                JsonSerializer.Serialize(writer, toolCallResult, options.GetTypeInfo(typeof(ToolCallResultEvent)));
                break;
            case StateSnapshotEvent stateSnapshot:
                JsonSerializer.Serialize(writer, stateSnapshot, options.GetTypeInfo(typeof(StateSnapshotEvent)));
                break;
            case StateDeltaEvent stateDelta:
                JsonSerializer.Serialize(writer, stateDelta, options.GetTypeInfo(typeof(StateDeltaEvent)));
                break;
            case StepStartedEvent stepStarted:
                JsonSerializer.Serialize(writer, stepStarted, options.GetTypeInfo(typeof(StepStartedEvent)));
                break;
            case StepFinishedEvent stepFinished:
                JsonSerializer.Serialize(writer, stepFinished, options.GetTypeInfo(typeof(StepFinishedEvent)));
                break;
            case ActivitySnapshotEvent activitySnapshot:
                JsonSerializer.Serialize(writer, activitySnapshot, options.GetTypeInfo(typeof(ActivitySnapshotEvent)));
                break;
            case ActivityDeltaEvent activityDelta:
                JsonSerializer.Serialize(writer, activityDelta, options.GetTypeInfo(typeof(ActivityDeltaEvent)));
                break;
            case MessagesSnapshotEvent messagesSnapshot:
                JsonSerializer.Serialize(writer, messagesSnapshot, options.GetTypeInfo(typeof(MessagesSnapshotEvent)));
                break;
            case ThinkingStartEvent thinkingStart:
                JsonSerializer.Serialize(writer, thinkingStart, options.GetTypeInfo(typeof(ThinkingStartEvent)));
                break;
            case ThinkingEndEvent thinkingEnd:
                JsonSerializer.Serialize(writer, thinkingEnd, options.GetTypeInfo(typeof(ThinkingEndEvent)));
                break;
            case ThinkingTextMessageStartEvent thinkingTextStart:
                JsonSerializer.Serialize(writer, thinkingTextStart, options.GetTypeInfo(typeof(ThinkingTextMessageStartEvent)));
                break;
            case ThinkingTextMessageContentEvent thinkingTextContent:
                JsonSerializer.Serialize(writer, thinkingTextContent, options.GetTypeInfo(typeof(ThinkingTextMessageContentEvent)));
                break;
            case ThinkingTextMessageEndEvent thinkingTextEnd:
                JsonSerializer.Serialize(writer, thinkingTextEnd, options.GetTypeInfo(typeof(ThinkingTextMessageEndEvent)));
                break;
            case RawEvent rawEvent:
                JsonSerializer.Serialize(writer, rawEvent, options.GetTypeInfo(typeof(RawEvent)));
                break;
            case CustomEvent customEvent:
                JsonSerializer.Serialize(writer, customEvent, options.GetTypeInfo(typeof(CustomEvent)));
                break;
            default:
                throw new InvalidOperationException($"Unknown event type: {value.GetType().Name}");
        }
    }
}
