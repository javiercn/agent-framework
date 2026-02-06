// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AGUI.Protocol.UnitTests;

/// <summary>
/// Tests for <see cref="RunAgentInput"/> serialization, specifically the <c>parentRunId</c> field
/// that clients use to indicate branching/time-travel scenarios.
/// </summary>
/// <remarks>
/// The <c>parentRunId</c> on <see cref="RunAgentInput"/> is the HTTP POST body sent TO the server,
/// which is separate from <see cref="RunStartedEvent.ParentRunId"/> that the server emits back
/// in the event stream. Both are needed for full branching support.
/// </remarks>
public class RunAgentInputSerializationTests
{
    private static readonly JsonSerializerOptions s_options = AGUIJsonSerializerContext.Default.Options;

    [Fact]
    public void ParentRunId_SerializesWhenSet()
    {
        // Arrange
        var input = new RunAgentInput
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            ParentRunId = "run_parent",
            Messages =
            [
                new AGUIUserMessage
                {
                    Id = "msg_1",
                    Content = [new AGUITextInputContent { Text = "Branch from here" }]
                }
            ]
        };

        // Act
        var json = JsonSerializer.Serialize(input, s_options);
        var deserialized = JsonSerializer.Deserialize<RunAgentInput>(json, s_options);

        // Assert
        json.Should().Contain("\"parentRunId\":\"run_parent\"");

        deserialized.Should().NotBeNull();
        deserialized!.ParentRunId.Should().Be("run_parent");
        deserialized.ThreadId.Should().Be("thread_123");
        deserialized.RunId.Should().Be("run_456");
    }

    [Fact]
    public void ParentRunId_OmittedWhenNull()
    {
        // Arrange
        var input = new RunAgentInput
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            ParentRunId = null,
            Messages = []
        };

        // Act
        var json = JsonSerializer.Serialize(input, s_options);

        // Assert
        json.Should().NotContain("parentRunId");
    }

    [Fact]
    public void ParentRunId_DeserializesFromClientJson()
    {
        // Arrange - JSON that a client would send in HTTP POST body
        const string json = """
        {
            "threadId": "thread_abc",
            "runId": "run_xyz",
            "parentRunId": "run_previous",
            "messages": [
                {
                    "id": "msg_1",
                    "role": "user",
                    "content": [{ "type": "text", "text": "Continue from branch" }]
                }
            ],
            "tools": [],
            "context": []
        }
        """;

        // Act
        var deserialized = JsonSerializer.Deserialize<RunAgentInput>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ThreadId.Should().Be("thread_abc");
        deserialized.RunId.Should().Be("run_xyz");
        deserialized.ParentRunId.Should().Be("run_previous");
        deserialized.Messages.Should().HaveCount(1);
    }

    [Fact]
    public void BranchingScenario_ClientSendsParentRunId()
    {
        // Arrange - Simulate a branching scenario where client wants to branch from run1
        var originalRun = new RunAgentInput
        {
            ThreadId = "thread_main",
            RunId = "run_001",
            ParentRunId = null, // First run has no parent
            Messages =
            [
                new AGUIUserMessage
                {
                    Id = "m1",
                    Content = [new AGUITextInputContent { Text = "Tell me about Paris" }]
                }
            ]
        };

        var branchRun = new RunAgentInput
        {
            ThreadId = "thread_main",
            RunId = "run_002",
            ParentRunId = "run_001", // Branching from run_001
            Messages =
            [
                new AGUIUserMessage
                {
                    Id = "m2",
                    Content = [new AGUITextInputContent { Text = "Actually, tell me about London instead" }]
                }
            ]
        };

        // Act
        var originalJson = JsonSerializer.Serialize(originalRun, s_options);
        var branchJson = JsonSerializer.Serialize(branchRun, s_options);

        var restoredOriginal = JsonSerializer.Deserialize<RunAgentInput>(originalJson, s_options);
        var restoredBranch = JsonSerializer.Deserialize<RunAgentInput>(branchJson, s_options);

        // Assert
        restoredOriginal.Should().NotBeNull();
        restoredOriginal!.ParentRunId.Should().BeNull();
        restoredOriginal.RunId.Should().Be("run_001");

        restoredBranch.Should().NotBeNull();
        restoredBranch!.ParentRunId.Should().Be("run_001");
        restoredBranch.RunId.Should().Be("run_002");

        // Verify JSON structure
        originalJson.Should().NotContain("parentRunId");
        branchJson.Should().Contain("\"parentRunId\":\"run_001\"");
    }

    [Fact]
    public void FullInput_WithAllFields_RoundTripsCorrectly()
    {
        // Arrange
        var state = JsonDocument.Parse("{\"counter\": 5}").RootElement;
        var forwardedProps = JsonDocument.Parse("{\"tenant\": \"acme\"}").RootElement;

        var input = new RunAgentInput
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            ParentRunId = "run_previous",
            State = state,
            Messages =
            [
                new AGUIUserMessage
                {
                    Id = "m1",
                    Content = [new AGUITextInputContent { Text = "Hello" }]
                }
            ],
            Tools =
            [
                new AGUITool
                {
                    Name = "get_weather",
                    Description = "Get weather for a location",
                    Parameters = JsonDocument.Parse("""{"type":"object"}""").RootElement
                }
            ],
            Context =
            [
                new AGUIContextItem
                {
                    Description = "User preferences",
                    Value = "prefers concise answers"
                }
            ],
            ForwardedProperties = forwardedProps,
            Resume = new AGUIResume
            {
                InterruptId = "interrupt_001",
                Payload = JsonDocument.Parse("{\"approved\": true}").RootElement
            }
        };

        // Act
        var json = JsonSerializer.Serialize(input, s_options);
        var deserialized = JsonSerializer.Deserialize<RunAgentInput>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ThreadId.Should().Be("thread_123");
        deserialized.RunId.Should().Be("run_456");
        deserialized.ParentRunId.Should().Be("run_previous");
        deserialized.State.GetProperty("counter").GetInt32().Should().Be(5);
        deserialized.Messages.Should().HaveCount(1);
        deserialized.Tools.Should().HaveCount(1);
        deserialized.Context.Should().HaveCount(1);
        deserialized.ForwardedProperties.GetProperty("tenant").GetString().Should().Be("acme");
        deserialized.Resume.Should().NotBeNull();
        deserialized.Resume!.InterruptId.Should().Be("interrupt_001");
    }

    [Fact]
    public void ParentRunId_WithResumeFromInterrupt_WorksCorrectly()
    {
        // Arrange - Resuming from an interrupt typically includes parentRunId
        // pointing to the run that was interrupted
        var input = new RunAgentInput
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            ParentRunId = "run_455", // The interrupted run
            Messages =
            [
                new AGUIUserMessage
                {
                    Id = "m1",
                    Content = [new AGUITextInputContent { Text = "Continue" }]
                }
            ],
            Resume = new AGUIResume
            {
                InterruptId = "interrupt_001",
                Payload = JsonDocument.Parse("{\"approved\": true}").RootElement
            }
        };

        // Act
        var json = JsonSerializer.Serialize(input, s_options);
        var deserialized = JsonSerializer.Deserialize<RunAgentInput>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ParentRunId.Should().Be("run_455");
        deserialized.Resume.Should().NotBeNull();
        deserialized.Resume!.InterruptId.Should().Be("interrupt_001");
    }
}
