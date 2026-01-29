// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AGUI.Protocol.UnitTests;

public class EventSerializationTests
{
    private static readonly JsonSerializerOptions s_options = AGUIJsonSerializerContext.Default.Options;

    [Fact]
    public void RunStartedEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new RunStartedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RunStartedEvent>();
        var result = (RunStartedEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.RunStarted);
        result.ThreadId.Should().Be("thread_123");
        result.RunId.Should().Be("run_456");
    }

    [Fact]
    public void RunFinishedEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new RunFinishedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RunFinishedEvent>();
        var result = (RunFinishedEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.RunFinished);
    }

    [Fact]
    public void RunErrorEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new RunErrorEvent
        {
            Message = "Something went wrong",
            Code = "ERR_001"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RunErrorEvent>();
        var result = (RunErrorEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.RunError);
        result.Message.Should().Be("Something went wrong");
        result.Code.Should().Be("ERR_001");
    }

    [Fact]
    public void TextMessageStartEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new TextMessageStartEvent
        {
            MessageId = "msg_123",
            Role = AGUIRoles.Assistant
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<TextMessageStartEvent>();
        var result = (TextMessageStartEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.TextMessageStart);
        result.MessageId.Should().Be("msg_123");
        result.Role.Should().Be(AGUIRoles.Assistant);
    }

    [Fact]
    public void TextMessageContentEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new TextMessageContentEvent
        {
            MessageId = "msg_123",
            Delta = "Hello "
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<TextMessageContentEvent>();
        var result = (TextMessageContentEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.TextMessageContent);
        result.MessageId.Should().Be("msg_123");
        result.Delta.Should().Be("Hello ");
    }

    [Fact]
    public void TextMessageEndEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new TextMessageEndEvent
        {
            MessageId = "msg_123"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<TextMessageEndEvent>();
        var result = (TextMessageEndEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.TextMessageEnd);
        result.MessageId.Should().Be("msg_123");
    }

    [Fact]
    public void ToolCallStartEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new ToolCallStartEvent
        {
            ToolCallId = "call_123",
            ToolCallName = "get_weather",
            ParentMessageId = "msg_456"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<ToolCallStartEvent>();
        var result = (ToolCallStartEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.ToolCallStart);
        result.ToolCallId.Should().Be("call_123");
        result.ToolCallName.Should().Be("get_weather");
        result.ParentMessageId.Should().Be("msg_456");
    }

    [Fact]
    public void ToolCallStartEvent_OmitsNullParentMessageId()
    {
        // Arrange
        var evt = new ToolCallStartEvent
        {
            ToolCallId = "call_123",
            ToolCallName = "get_weather",
            ParentMessageId = null
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);

        // Assert
        json.Should().NotContain("parentMessageId");
    }

    [Fact]
    public void ToolCallArgsEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new ToolCallArgsEvent
        {
            ToolCallId = "call_123",
            Delta = "{\"city\":"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<ToolCallArgsEvent>();
        var result = (ToolCallArgsEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.ToolCallArgs);
        result.ToolCallId.Should().Be("call_123");
    }

    [Fact]
    public void StepStartedEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new StepStartedEvent
        {
            StepId = "step_123",
            StepName = "Processing",
            ParentStepId = null
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<StepStartedEvent>();
        var result = (StepStartedEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.StepStarted);
        result.StepId.Should().Be("step_123");
        result.StepName.Should().Be("Processing");
    }

    [Fact]
    public void StepFinishedEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new StepFinishedEvent
        {
            StepId = "step_123",
            Status = "completed"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<StepFinishedEvent>();
        var result = (StepFinishedEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.StepFinished);
        result.StepId.Should().Be("step_123");
        result.Status.Should().Be("completed");
    }

    [Fact]
    public void ThinkingStartEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new ThinkingStartEvent();

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<ThinkingStartEvent>();
        var result = (ThinkingStartEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.ThinkingStart);
    }

    [Fact]
    public void ThinkingEndEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new ThinkingEndEvent();

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<ThinkingEndEvent>();
        var result = (ThinkingEndEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.ThinkingEnd);
    }

    [Fact]
    public void ThinkingTextMessageContentEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new ThinkingTextMessageContentEvent
        {
            MessageId = "msg_123",
            Delta = "Let me think..."
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<ThinkingTextMessageContentEvent>();
        var result = (ThinkingTextMessageContentEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.ThinkingTextMessageContent);
        result.MessageId.Should().Be("msg_123");
        result.Delta.Should().Be("Let me think...");
    }

    [Fact]
    public void StateSnapshotEvent_SerializesCorrectly()
    {
        // Arrange
        var state = JsonDocument.Parse("{\"counter\": 42}").RootElement;
        var evt = new StateSnapshotEvent
        {
            Snapshot = state
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<StateSnapshotEvent>();
        var result = (StateSnapshotEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.StateSnapshot);
        result.Snapshot.Should().NotBeNull();
    }

    [Fact]
    public void CustomEvent_SerializesCorrectly()
    {
        // Arrange
        var value = JsonDocument.Parse("{\"data\": \"test\"}").RootElement;
        var evt = new CustomEvent
        {
            Name = "my_custom_event",
            Value = value
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<CustomEvent>();
        var result = (CustomEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.Custom);
        result.Name.Should().Be("my_custom_event");
    }

    [Fact]
    public void MessagesSnapshotEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new MessagesSnapshotEvent
        {
            Messages =
            [
                new AGUIUserMessage { Id = "msg_1", Content = "Hello" },
                new AGUIAssistantMessage { Id = "msg_2", Content = "Hi there!" }
            ]
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<MessagesSnapshotEvent>();
        var result = (MessagesSnapshotEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.MessagesSnapshot);
        result.Messages.Should().HaveCount(2);
        result.Messages[0].Should().BeOfType<AGUIUserMessage>();
        result.Messages[1].Should().BeOfType<AGUIAssistantMessage>();
    }

    [Fact]
    public void ActivitySnapshotEvent_SerializesCorrectly()
    {
        // Arrange
        var state = JsonDocument.Parse("{\"progress\": 50}").RootElement;
        var evt = new ActivitySnapshotEvent
        {
            ActivityId = "activity_123",
            ActivityType = "file_upload",
            State = state
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<ActivitySnapshotEvent>();
        var result = (ActivitySnapshotEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.ActivitySnapshot);
        result.ActivityId.Should().Be("activity_123");
        result.ActivityType.Should().Be("file_upload");
    }

    [Fact]
    public void ActivityDeltaEvent_SerializesCorrectly()
    {
        // Arrange
        var delta = JsonDocument.Parse("{\"progress\": 75}").RootElement;
        var evt = new ActivityDeltaEvent
        {
            ActivityId = "activity_123",
            Delta = delta
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<ActivityDeltaEvent>();
        var result = (ActivityDeltaEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.ActivityDelta);
        result.ActivityId.Should().Be("activity_123");
    }

    [Fact]
    public void RawEvent_SerializesCorrectly()
    {
        // Arrange
        var rawData = JsonDocument.Parse("{\"openai_response\": \"test\"}").RootElement;
        var evt = new RawEvent
        {
            Source = "openai",
            Event = rawData
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RawEvent>();
        var result = (RawEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.Raw);
        result.Source.Should().Be("openai");
    }

    [Fact]
    public void ToolCallEndEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new ToolCallEndEvent
        {
            ToolCallId = "call_123"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<ToolCallEndEvent>();
        var result = (ToolCallEndEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.ToolCallEnd);
        result.ToolCallId.Should().Be("call_123");
    }

    [Fact]
    public void ToolCallResultEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new ToolCallResultEvent
        {
            ToolCallId = "call_123",
            Result = "72°F"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<ToolCallResultEvent>();
        var result = (ToolCallResultEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.ToolCallResult);
        result.ToolCallId.Should().Be("call_123");
        result.Result.Should().Be("72°F");
    }

    [Fact]
    public void StateDeltaEvent_SerializesCorrectly()
    {
        // Arrange
        var delta = JsonDocument.Parse("{\"counter\": 43}").RootElement;
        var evt = new StateDeltaEvent
        {
            Delta = delta
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<StateDeltaEvent>();
        var result = (StateDeltaEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.StateDelta);
    }

    [Fact]
    public void ThinkingTextMessageStartEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new ThinkingTextMessageStartEvent
        {
            MessageId = "msg_thinking_1"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<ThinkingTextMessageStartEvent>();
        var result = (ThinkingTextMessageStartEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.ThinkingTextMessageStart);
        result.MessageId.Should().Be("msg_thinking_1");
    }

    [Fact]
    public void ThinkingTextMessageEndEvent_SerializesCorrectly()
    {
        // Arrange
        var evt = new ThinkingTextMessageEndEvent
        {
            MessageId = "msg_thinking_1"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<ThinkingTextMessageEndEvent>();
        var result = (ThinkingTextMessageEndEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.ThinkingTextMessageEnd);
        result.MessageId.Should().Be("msg_thinking_1");
    }
}
