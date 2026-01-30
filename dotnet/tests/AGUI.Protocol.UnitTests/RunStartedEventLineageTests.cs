// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AGUI.Protocol.UnitTests;

public class RunStartedEventLineageTests
{
    private static readonly JsonSerializerOptions s_options = AGUIJsonSerializerContext.Default.Options;

    [Fact]
    public void ParentRunId_SerializesWhenSet()
    {
        // Arrange
        var evt = new RunStartedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            ParentRunId = "run_parent"
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        json.Should().Contain("\"parentRunId\":\"run_parent\"");

        deserialized.Should().BeOfType<RunStartedEvent>();
        var result = (RunStartedEvent)deserialized!;
        result.ParentRunId.Should().Be("run_parent");
    }

    [Fact]
    public void ParentRunId_OmittedWhenNull()
    {
        // Arrange
        var evt = new RunStartedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            ParentRunId = null
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);

        // Assert
        json.Should().NotContain("parentRunId");
    }

    [Fact]
    public void Input_SerializesWhenSet()
    {
        // Arrange
        var evt = new RunStartedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            Input = new RunAgentInput
            {
                ThreadId = "thread_123",
                RunId = "run_456",
                Messages =
                [
                    new AGUIUserMessage
                    {
                        Id = "msg_1",
                        Content = [new AGUITextInputContent { Text = "Hello" }]
                    }
                ]
            }
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        json.Should().Contain("\"input\":");
        json.Should().Contain("\"messages\":");

        deserialized.Should().BeOfType<RunStartedEvent>();
        var result = (RunStartedEvent)deserialized!;
        result.Input.Should().NotBeNull();
        result.Input!.ThreadId.Should().Be("thread_123");
        result.Input.Messages.Should().HaveCount(1);
        result.Input.Messages.First().Should().BeOfType<AGUIUserMessage>();
    }

    [Fact]
    public void Input_OmittedWhenNull()
    {
        // Arrange
        var evt = new RunStartedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            Input = null
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);

        // Assert
        json.Should().NotContain("\"input\"");
    }

    [Fact]
    public void BranchingScenario_PreservesLineage()
    {
        // Arrange - Create a branching scenario with parent-child relationship
        var parentRun = new RunStartedEvent
        {
            ThreadId = "thread_main",
            RunId = "run_001",
            Input = new RunAgentInput
            {
                ThreadId = "thread_main",
                RunId = "run_001",
                Messages = [new AGUIUserMessage { Id = "m1", Content = [new AGUITextInputContent { Text = "Start conversation" }] }]
            }
        };

        var childRun = new RunStartedEvent
        {
            ThreadId = "thread_main",
            RunId = "run_002",
            ParentRunId = "run_001",
            Input = new RunAgentInput
            {
                ThreadId = "thread_main",
                RunId = "run_002",
                Messages = [new AGUIUserMessage { Id = "m2", Content = [new AGUITextInputContent { Text = "Continue from branch" }] }]
            }
        };

        var events = new BaseEvent[] { parentRun, childRun };

        // Act
        var json = JsonSerializer.Serialize(events, s_options);
        var restored = JsonSerializer.Deserialize<BaseEvent[]>(json, s_options);

        // Assert
        restored.Should().NotBeNull();
        restored.Should().HaveCount(2);

        var restoredParent = (RunStartedEvent)restored![0];
        restoredParent.RunId.Should().Be("run_001");
        restoredParent.ParentRunId.Should().BeNull();
        restoredParent.Input.Should().NotBeNull();

        var restoredChild = (RunStartedEvent)restored[1];
        restoredChild.RunId.Should().Be("run_002");
        restoredChild.ParentRunId.Should().Be("run_001");
        restoredChild.Input.Should().NotBeNull();
    }

    [Fact]
    public void Input_WithTools_SerializesCorrectly()
    {
        // Arrange
        var evt = new RunStartedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            Input = new RunAgentInput
            {
                ThreadId = "thread_123",
                RunId = "run_456",
                Messages = [new AGUIUserMessage { Id = "m1", Content = [new AGUITextInputContent { Text = "Get weather" }] }],
                Tools =
                [
                    new AGUITool
                    {
                        Name = "get_weather",
                        Description = "Get weather for a location",
                        Parameters = JsonDocument.Parse("""{"type":"object","properties":{"location":{"type":"string"}}}""").RootElement
                    }
                ]
            }
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RunStartedEvent>();
        var result = (RunStartedEvent)deserialized!;
        result.Input.Should().NotBeNull();
        result.Input!.Tools.Should().HaveCount(1);
        result.Input.Tools!.First().Name.Should().Be("get_weather");
    }

    [Fact]
    public void Input_WithContext_SerializesCorrectly()
    {
        // Arrange
        var evt = new RunStartedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            Input = new RunAgentInput
            {
                ThreadId = "thread_123",
                RunId = "run_456",
                Messages = [new AGUIUserMessage { Id = "m1", Content = [new AGUITextInputContent { Text = "Help me" }] }],
                Context =
                [
                    new AGUIContextItem
                    {
                        Description = "User preferences",
                        Value = "prefers concise answers"
                    }
                ]
            }
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RunStartedEvent>();
        var result = (RunStartedEvent)deserialized!;
        result.Input.Should().NotBeNull();
        result.Input!.Context.Should().HaveCount(1);
        result.Input.Context![0].Description.Should().Be("User preferences");
        result.Input.Context[0].Value.Should().Be("prefers concise answers");
    }

    [Fact]
    public void Input_WithState_SerializesCorrectly()
    {
        // Arrange
        var state = JsonDocument.Parse("{\"counter\": 5, \"theme\": \"dark\"}").RootElement;
        var evt = new RunStartedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            Input = new RunAgentInput
            {
                ThreadId = "thread_123",
                RunId = "run_456",
                Messages = [new AGUIUserMessage { Id = "m1", Content = [new AGUITextInputContent { Text = "Continue" }] }],
                State = state
            }
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RunStartedEvent>();
        var result = (RunStartedEvent)deserialized!;
        result.Input.Should().NotBeNull();
        result.Input!.State.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        result.Input.State.GetProperty("counter").GetInt32().Should().Be(5);
        result.Input.State.GetProperty("theme").GetString().Should().Be("dark");
    }

    [Fact]
    public void Input_WithResume_SerializesCorrectly()
    {
        // Arrange - Resume from an interrupt
        var payload = JsonDocument.Parse("{\"approved\": true}").RootElement;
        var evt = new RunStartedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            ParentRunId = "run_455", // Previous run that was interrupted
            Input = new RunAgentInput
            {
                ThreadId = "thread_123",
                RunId = "run_456",
                Messages = [new AGUIUserMessage { Id = "m1", Content = [new AGUITextInputContent { Text = "Continue" }] }],
                Resume = new AGUIResume
                {
                    InterruptId = "interrupt_001",
                    Payload = payload
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RunStartedEvent>();
        var result = (RunStartedEvent)deserialized!;
        result.ParentRunId.Should().Be("run_455");
        result.Input.Should().NotBeNull();
        result.Input!.Resume.Should().NotBeNull();
        result.Input.Resume!.InterruptId.Should().Be("interrupt_001");
        result.Input.Resume.Payload.Should().NotBeNull();
        result.Input.Resume.Payload!.Value.GetProperty("approved").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void DeserializeFromJson_WithAllFields_WorksCorrectly()
    {
        // Arrange - JSON that might come from TypeScript SDK
        const string json = """
        {
            "type": "RUN_STARTED",
            "threadId": "thread_abc",
            "runId": "run_xyz",
            "parentRunId": "run_previous",
            "input": {
                "threadId": "thread_abc",
                "runId": "run_xyz",
                "messages": [
                    {
                        "id": "msg_1",
                        "role": "user",
                        "content": [{ "type": "text", "text": "Hello" }]
                    }
                ],
                "tools": [],
                "context": []
            }
        }
        """;

        // Act
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RunStartedEvent>();
        var result = (RunStartedEvent)deserialized!;
        result.ThreadId.Should().Be("thread_abc");
        result.RunId.Should().Be("run_xyz");
        result.ParentRunId.Should().Be("run_previous");
        result.Input.Should().NotBeNull();
        result.Input!.Messages.Should().HaveCount(1);
    }

    [Fact]
    public void DeserializeFromJson_WithoutOptionalFields_WorksCorrectly()
    {
        // Arrange - Minimal JSON without optional fields
        const string json = """
        {
            "type": "RUN_STARTED",
            "threadId": "thread_abc",
            "runId": "run_xyz"
        }
        """;

        // Act
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RunStartedEvent>();
        var result = (RunStartedEvent)deserialized!;
        result.ThreadId.Should().Be("thread_abc");
        result.RunId.Should().Be("run_xyz");
        result.ParentRunId.Should().BeNull();
        result.Input.Should().BeNull();
    }
}
