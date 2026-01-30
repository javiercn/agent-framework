// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AGUI.Protocol.UnitTests;

public class EventArraySerializationTests
{
    private static readonly JsonSerializerOptions s_options = AGUIJsonSerializerContext.Default.Options;

    [Fact]
    public void SerializeDeserialize_EventArray_RoundTripsCorrectly()
    {
        // Arrange
        var events = new BaseEvent[]
        {
            new RunStartedEvent { ThreadId = "thread_1", RunId = "run_1" },
            new TextMessageStartEvent { MessageId = "msg_1", Role = AGUIRoles.Assistant },
            new TextMessageContentEvent { MessageId = "msg_1", Delta = "Hello " },
            new TextMessageContentEvent { MessageId = "msg_1", Delta = "World!" },
            new TextMessageEndEvent { MessageId = "msg_1" },
            new RunFinishedEvent { ThreadId = "thread_1", RunId = "run_1" }
        };

        // Act
        var json = JsonSerializer.Serialize(events, s_options);
        var restored = JsonSerializer.Deserialize<BaseEvent[]>(json, s_options);

        // Assert
        restored.Should().NotBeNull();
        restored.Should().HaveCount(6);
        restored![0].Should().BeOfType<RunStartedEvent>();
        restored[1].Should().BeOfType<TextMessageStartEvent>();
        restored[2].Should().BeOfType<TextMessageContentEvent>();
        restored[3].Should().BeOfType<TextMessageContentEvent>();
        restored[4].Should().BeOfType<TextMessageEndEvent>();
        restored[5].Should().BeOfType<RunFinishedEvent>();
    }

    [Fact]
    public void SerializeDeserialize_MixedEventTypes_PreservesPolymorphism()
    {
        // Arrange
        var events = new BaseEvent[]
        {
            new RunStartedEvent { ThreadId = "t", RunId = "r" },
            new StepStartedEvent { StepId = "s1", StepName = "Step 1" },
            new ToolCallStartEvent { ToolCallId = "tc1", ToolCallName = "get_weather" },
            new ToolCallArgsEvent { ToolCallId = "tc1", Delta = "{\"city\":\"Seattle\"}" },
            new ToolCallEndEvent { ToolCallId = "tc1" },
            new ToolCallResultEvent { ToolCallId = "tc1", Result = "72°F" },
            new StepFinishedEvent { StepId = "s1" },
            new TextMessageStartEvent { MessageId = "m1", Role = AGUIRoles.Assistant },
            new TextMessageContentEvent { MessageId = "m1", Delta = "The weather is 72°F" },
            new TextMessageEndEvent { MessageId = "m1" },
            new RunFinishedEvent { ThreadId = "t", RunId = "r" }
        };

        // Act
        var json = JsonSerializer.Serialize(events, s_options);
        var restored = JsonSerializer.Deserialize<BaseEvent[]>(json, s_options);

        // Assert
        restored.Should().NotBeNull();
        restored.Should().HaveCount(11);

        // Verify types are preserved
        restored![0].Type.Should().Be(AGUIEventTypes.RunStarted);
        restored[1].Type.Should().Be(AGUIEventTypes.StepStarted);
        restored[2].Type.Should().Be(AGUIEventTypes.ToolCallStart);
        restored[3].Type.Should().Be(AGUIEventTypes.ToolCallArgs);
        restored[4].Type.Should().Be(AGUIEventTypes.ToolCallEnd);
        restored[5].Type.Should().Be(AGUIEventTypes.ToolCallResult);
        restored[6].Type.Should().Be(AGUIEventTypes.StepFinished);
        restored[7].Type.Should().Be(AGUIEventTypes.TextMessageStart);
        restored[8].Type.Should().Be(AGUIEventTypes.TextMessageContent);
        restored[9].Type.Should().Be(AGUIEventTypes.TextMessageEnd);
        restored[10].Type.Should().Be(AGUIEventTypes.RunFinished);

        // Verify specific properties
        ((ToolCallStartEvent)restored[2]).ToolCallName.Should().Be("get_weather");
        ((TextMessageContentEvent)restored[8]).Delta.Should().Be("The weather is 72°F");
    }

    [Fact]
#pragma warning disable CA1825 // Avoid unnecessary zero-length array allocations
    public void SerializeDeserialize_EmptyArray_ReturnsEmpty()
    {
        // Arrange
        var events = new BaseEvent[0];
#pragma warning restore CA1825

        // Act
        var json = JsonSerializer.Serialize(events, s_options);
        var restored = JsonSerializer.Deserialize<BaseEvent[]>(json, s_options);

        // Assert
        restored.Should().NotBeNull();
        restored.Should().BeEmpty();
    }

    [Fact]
    public void SerializeDeserialize_AllEventTypes_RoundTrip()
    {
        // Arrange - include all event types
        var stateSnapshot = JsonDocument.Parse("{\"counter\": 1}").RootElement;
        var stateDelta = JsonDocument.Parse("[{\"op\":\"replace\",\"path\":\"/counter\",\"value\":2}]").RootElement;
        var activityState = JsonDocument.Parse("{\"progress\": 50}").RootElement;
        var activityDelta = JsonDocument.Parse("{\"progress\": 75}").RootElement;
        var customValue = JsonDocument.Parse("{\"custom\": true}").RootElement;
        var rawData = JsonDocument.Parse("{\"source_event\": \"test\"}").RootElement;

        var events = new BaseEvent[]
        {
            new RunStartedEvent { ThreadId = "t", RunId = "r" },
            new RunFinishedEvent { ThreadId = "t", RunId = "r" },
            new RunErrorEvent { Message = "Error", Code = "E001" },
            new StepStartedEvent { StepId = "s1", StepName = "Step" },
            new StepFinishedEvent { StepId = "s1" },
            new TextMessageStartEvent { MessageId = "m1", Role = AGUIRoles.Assistant },
            new TextMessageContentEvent { MessageId = "m1", Delta = "text" },
            new TextMessageEndEvent { MessageId = "m1" },
            new ToolCallStartEvent { ToolCallId = "tc1", ToolCallName = "tool" },
            new ToolCallArgsEvent { ToolCallId = "tc1", Delta = "{}" },
            new ToolCallEndEvent { ToolCallId = "tc1" },
            new ToolCallResultEvent { ToolCallId = "tc1", Result = "result" },
            new StateSnapshotEvent { Snapshot = stateSnapshot },
            new StateDeltaEvent { Delta = stateDelta },
            new MessagesSnapshotEvent { Messages = [new AGUIUserMessage { Id = "u1", Content = [new AGUITextInputContent { Text = "Hi" }] }] },
            new ActivitySnapshotEvent { ActivityId = "a1", ActivityType = "progress", State = activityState },
            new ActivityDeltaEvent { ActivityId = "a1", Delta = activityDelta },
            new ReasoningStartEvent { MessageId = "r1" },
            new ReasoningMessageStartEvent { MessageId = "rm1", Role = AGUIRoles.Assistant },
            new ReasoningMessageContentEvent { MessageId = "rm1", Delta = "thinking" },
            new ReasoningMessageEndEvent { MessageId = "rm1" },
            new ReasoningMessageChunkEvent { MessageId = "rc1", Delta = "chunk" },
            new ReasoningEndEvent { MessageId = "r1" },
            new CustomEvent { Name = "custom", Value = customValue },
            new RawEvent { Source = "test", Event = rawData }
        };

        // Act
        var json = JsonSerializer.Serialize(events, s_options);
        var restored = JsonSerializer.Deserialize<BaseEvent[]>(json, s_options);

        // Assert
        restored.Should().NotBeNull();
        restored.Should().HaveCount(events.Length);

        for (int i = 0; i < events.Length; i++)
        {
            restored![i].GetType().Should().Be(events[i].GetType(), $"Event at index {i} should have correct type");
            restored[i].Type.Should().Be(events[i].Type, $"Event at index {i} should have correct Type property");
        }
    }

    [Fact]
    public void SerializeDeserialize_WithNullableFields_HandlesCorrectly()
    {
        // Arrange
        var events = new BaseEvent[]
        {
            new RunStartedEvent
            {
                ThreadId = "t",
                RunId = "r",
                ParentRunId = "parent_r",
                Input = new RunAgentInput
                {
                    ThreadId = "t",
                    RunId = "r",
                    Messages = [new AGUIUserMessage { Id = "m1", Content = [new AGUITextInputContent { Text = "Test" }] }]
                }
            },
            new RunStartedEvent
            {
                ThreadId = "t2",
                RunId = "r2",
                ParentRunId = null, // Should be omitted in JSON
                Input = null // Should be omitted in JSON
            },
            new ToolCallStartEvent
            {
                ToolCallId = "tc1",
                ToolCallName = "tool",
                ParentMessageId = "pm1"
            },
            new ToolCallStartEvent
            {
                ToolCallId = "tc2",
                ToolCallName = "tool2",
                ParentMessageId = null // Should be omitted in JSON
            },
            new RunFinishedEvent
            {
                ThreadId = "t",
                RunId = "r",
                Outcome = "interrupt",
                Interrupt = new AGUIInterrupt { Id = "int1" }
            },
            new RunFinishedEvent
            {
                ThreadId = "t2",
                RunId = "r2",
                Outcome = null,
                Interrupt = null
            }
        };

        // Act
        var json = JsonSerializer.Serialize(events, s_options);
        var restored = JsonSerializer.Deserialize<BaseEvent[]>(json, s_options);

        // Assert
        restored.Should().NotBeNull();
        restored.Should().HaveCount(6);

        // First RunStartedEvent should have ParentRunId and Input
        var rs1 = restored![0].Should().BeOfType<RunStartedEvent>().Subject;
        rs1.ParentRunId.Should().Be("parent_r");
        rs1.Input.Should().NotBeNull();
        rs1.Input!.ThreadId.Should().Be("t");

        // Second RunStartedEvent should have null ParentRunId and Input
        var rs2 = restored[1].Should().BeOfType<RunStartedEvent>().Subject;
        rs2.ParentRunId.Should().BeNull();
        rs2.Input.Should().BeNull();

        // First ToolCallStartEvent should have ParentMessageId
        var tc1 = restored[2].Should().BeOfType<ToolCallStartEvent>().Subject;
        tc1.ParentMessageId.Should().Be("pm1");

        // Second ToolCallStartEvent should have null ParentMessageId
        var tc2 = restored[3].Should().BeOfType<ToolCallStartEvent>().Subject;
        tc2.ParentMessageId.Should().BeNull();

        // First RunFinishedEvent should have Outcome and Interrupt
        var rf1 = restored[4].Should().BeOfType<RunFinishedEvent>().Subject;
        rf1.Outcome.Should().Be("interrupt");
        rf1.Interrupt.Should().NotBeNull();

        // Second RunFinishedEvent should have null Outcome and Interrupt
        var rf2 = restored[5].Should().BeOfType<RunFinishedEvent>().Subject;
        rf2.Outcome.Should().BeNull();
        rf2.Interrupt.Should().BeNull();
    }

    [Fact]
    public void FullConversation_SerializeDeserializeRoundTrip()
    {
        // Arrange - a full conversation with branching
        var events = new BaseEvent[]
        {
            // Initial run
            new RunStartedEvent { ThreadId = "thread_abc", RunId = "run_001" },
            new TextMessageStartEvent { MessageId = "msg_001", Role = AGUIRoles.Assistant },
            new TextMessageContentEvent { MessageId = "msg_001", Delta = "Hello! How can I help you today?" },
            new TextMessageEndEvent { MessageId = "msg_001" },
            new RunFinishedEvent { ThreadId = "thread_abc", RunId = "run_001" },

            // Second run (continuation)
            new RunStartedEvent { ThreadId = "thread_abc", RunId = "run_002", ParentRunId = "run_001" },
            new TextMessageStartEvent { MessageId = "msg_002", Role = AGUIRoles.Assistant },
            new TextMessageContentEvent { MessageId = "msg_002", Delta = "I understand you need help with weather." },
            new TextMessageEndEvent { MessageId = "msg_002" },
            new ToolCallStartEvent { ToolCallId = "call_001", ToolCallName = "get_weather", ParentMessageId = "msg_002" },
            new ToolCallArgsEvent { ToolCallId = "call_001", Delta = "{\"location\":\"Seattle\"}" },
            new ToolCallEndEvent { ToolCallId = "call_001" },
            new ToolCallResultEvent { ToolCallId = "call_001", Result = "72°F, Sunny" },
            new TextMessageStartEvent { MessageId = "msg_003", Role = AGUIRoles.Assistant },
            new TextMessageContentEvent { MessageId = "msg_003", Delta = "The weather in Seattle is 72°F and Sunny!" },
            new TextMessageEndEvent { MessageId = "msg_003" },
            new RunFinishedEvent { ThreadId = "thread_abc", RunId = "run_002" },

            // Branch from run_001 (alternative path)
            new RunStartedEvent { ThreadId = "thread_abc", RunId = "run_003", ParentRunId = "run_001" },
            new TextMessageStartEvent { MessageId = "msg_004", Role = AGUIRoles.Assistant },
            new TextMessageContentEvent { MessageId = "msg_004", Delta = "Let me help you with something else instead." },
            new TextMessageEndEvent { MessageId = "msg_004" },
            new RunFinishedEvent { ThreadId = "thread_abc", RunId = "run_003" }
        };

        // Act
        var json = JsonSerializer.Serialize(events, s_options);
        var restored = JsonSerializer.Deserialize<BaseEvent[]>(json, s_options);

        // Assert
        restored.Should().NotBeNull();
        restored.Should().HaveCount(events.Length);

        // Verify branching information is preserved
        var run1 = (RunStartedEvent)restored![0];
        run1.ParentRunId.Should().BeNull();

        var run2 = (RunStartedEvent)restored[5];
        run2.ParentRunId.Should().Be("run_001");

        var run3 = (RunStartedEvent)restored[17];
        run3.ParentRunId.Should().Be("run_001");
    }

    [Fact]
    public void BranchingScenario_WithParentRunId_PreservesLineage()
    {
        // Arrange
        var originalRun = new RunStartedEvent
        {
            ThreadId = "thread_1",
            RunId = "run_main"
        };

        var branchRun = new RunStartedEvent
        {
            ThreadId = "thread_1",
            RunId = "run_branch",
            ParentRunId = "run_main"
        };

        var events = new BaseEvent[] { originalRun, branchRun };

        // Act
        var json = JsonSerializer.Serialize(events, s_options);

        // Verify JSON structure
        json.Should().Contain("\"parentRunId\":\"run_main\"");
        json.Should().NotContain("\"parentRunId\":null"); // null should be omitted

        var restored = JsonSerializer.Deserialize<BaseEvent[]>(json, s_options);

        // Assert
        restored.Should().NotBeNull();
        restored.Should().HaveCount(2);

        var restoredOriginal = (RunStartedEvent)restored![0];
        restoredOriginal.ParentRunId.Should().BeNull();

        var restoredBranch = (RunStartedEvent)restored[1];
        restoredBranch.ParentRunId.Should().Be("run_main");
    }
}
