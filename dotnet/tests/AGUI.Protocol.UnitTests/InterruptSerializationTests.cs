// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AGUI.Protocol.UnitTests;

public class InterruptSerializationTests
{
    private static readonly JsonSerializerOptions s_options = AGUIJsonSerializerContext.Default.Options;

    [Fact]
    public void RunFinishedEvent_WithInterrupt_SerializesCorrectly()
    {
        // Arrange
        var interruptPayload = JsonDocument.Parse("""{"functionName":"get_weather","functionArguments":{"location":"Seattle"}}""").RootElement;
        var evt = new RunFinishedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            Outcome = RunFinishedOutcome.Interrupt,
            Interrupt = new AGUIInterrupt
            {
                Id = "call_abc",
                Payload = interruptPayload
            }
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RunFinishedEvent>();
        var result = (RunFinishedEvent)deserialized!;
        result.Type.Should().Be(AGUIEventTypes.RunFinished);
        result.ThreadId.Should().Be("thread_123");
        result.RunId.Should().Be("run_456");
        result.Outcome.Should().Be(RunFinishedOutcome.Interrupt);
        result.Interrupt.Should().NotBeNull();
        result.Interrupt!.Id.Should().Be("call_abc");
        result.Interrupt.Payload.Should().NotBeNull();
        result.Interrupt.Payload!.Value.GetProperty("functionName").GetString().Should().Be("get_weather");
    }

    [Fact]
    public void RunFinishedEvent_WithSuccess_SerializesCorrectly()
    {
        // Arrange
        var resultPayload = JsonDocument.Parse("""{"status":"completed","data":"some result"}""").RootElement;
        var evt = new RunFinishedEvent
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            Outcome = RunFinishedOutcome.Success,
            Result = resultPayload
        };

        // Act
        var json = JsonSerializer.Serialize(evt, s_options);
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<RunFinishedEvent>();
        var result = (RunFinishedEvent)deserialized!;
        result.Outcome.Should().Be(RunFinishedOutcome.Success);
        result.Result.Should().NotBeNull();
        result.Result!.Value.GetProperty("status").GetString().Should().Be("completed");
        result.Interrupt.Should().BeNull();
    }

    [Fact]
    public void RunFinishedEvent_WithoutOutcome_BackwardCompatible()
    {
        // Arrange - simulate an old-style event without outcome
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
        result.Outcome.Should().BeNull();
        result.Result.Should().BeNull();
        result.Interrupt.Should().BeNull();
    }

    [Fact]
    public void AGUIInterrupt_AllFields_SerializesCorrectly()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"prompt":"Enter your email","inputType":"email"}""").RootElement;
        var interrupt = new AGUIInterrupt
        {
            Id = "input_789",
            Payload = payload
        };

        // Act
        var json = JsonSerializer.Serialize(interrupt, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIInterrupt>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("input_789");
        deserialized.Payload.Should().NotBeNull();
        deserialized.Payload!.Value.GetProperty("prompt").GetString().Should().Be("Enter your email");
        deserialized.Payload!.Value.GetProperty("inputType").GetString().Should().Be("email");
    }

    [Fact]
    public void AGUIInterrupt_OnlyId_SerializesCorrectly()
    {
        // Arrange
        var interrupt = new AGUIInterrupt
        {
            Id = "interrupt_only_id"
        };

        // Act
        var json = JsonSerializer.Serialize(interrupt, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIInterrupt>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("interrupt_only_id");
        deserialized.Payload.Should().BeNull();

        // Verify JSON doesn't contain null payload
        json.Should().NotContain("payload");
    }

    [Fact]
    public void AGUIResume_WithPayload_SerializesCorrectly()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"approved":true}""").RootElement;
        var resume = new AGUIResume
        {
            InterruptId = "call_abc",
            Payload = payload
        };

        // Act
        var json = JsonSerializer.Serialize(resume, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIResume>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.InterruptId.Should().Be("call_abc");
        deserialized.Payload.Should().NotBeNull();
        deserialized.Payload!.Value.GetProperty("approved").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void AGUIResume_WithUserInput_SerializesCorrectly()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"response":"user@example.com"}""").RootElement;
        var resume = new AGUIResume
        {
            InterruptId = "input_789",
            Payload = payload
        };

        // Act
        var json = JsonSerializer.Serialize(resume, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIResume>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.InterruptId.Should().Be("input_789");
        deserialized.Payload.Should().NotBeNull();
        deserialized.Payload!.Value.GetProperty("response").GetString().Should().Be("user@example.com");
    }

    [Fact]
    public void RunAgentInput_WithResume_SerializesCorrectly()
    {
        // Arrange
        var resumePayload = JsonDocument.Parse("""{"approved":true}""").RootElement;
        var input = new RunAgentInput
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            Messages = [new AGUIUserMessage { Id = "msg_1", Content = [new AGUITextInputContent { Text = "Hello" }] }],
            Resume = new AGUIResume
            {
                InterruptId = "call_abc",
                Payload = resumePayload
            }
        };

        // Act
        var json = JsonSerializer.Serialize(input, s_options);
        var deserialized = JsonSerializer.Deserialize<RunAgentInput>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ThreadId.Should().Be("thread_123");
        deserialized.RunId.Should().Be("run_456");
        deserialized.Resume.Should().NotBeNull();
        deserialized.Resume!.InterruptId.Should().Be("call_abc");
        deserialized.Resume.Payload.Should().NotBeNull();
        deserialized.Resume.Payload!.Value.GetProperty("approved").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void RunAgentInput_WithoutResume_SerializesCorrectly()
    {
        // Arrange
        var input = new RunAgentInput
        {
            ThreadId = "thread_123",
            RunId = "run_456",
            Messages = [new AGUIUserMessage { Id = "msg_1", Content = [new AGUITextInputContent { Text = "Hello" }] }]
        };

        // Act
        var json = JsonSerializer.Serialize(input, s_options);
        var deserialized = JsonSerializer.Deserialize<RunAgentInput>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ThreadId.Should().Be("thread_123");
        deserialized.Resume.Should().BeNull();

        // Verify JSON doesn't contain null resume
        json.Should().NotContain("resume");
    }

    [Fact]
    public void RunFinishedEvent_Roundtrip_FunctionApprovalInterrupt()
    {
        // Arrange - simulates a function approval interrupt
        const string originalJson = """
            {
                "type": "RUN_FINISHED",
                "threadId": "thread_123",
                "runId": "run_456",
                "outcome": "interrupt",
                "interrupt": {
                    "id": "call_abc",
                    "payload": {
                        "functionName": "get_weather",
                        "functionArguments": {
                            "location": "Seattle",
                            "units": "fahrenheit"
                        }
                    }
                }
            }
            """;

        // Act
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(originalJson, s_options);
        var reserialized = JsonSerializer.Serialize(deserialized, s_options);
        var final = JsonSerializer.Deserialize<BaseEvent>(reserialized, s_options);

        // Assert
        final.Should().BeOfType<RunFinishedEvent>();
        var result = (RunFinishedEvent)final!;
        result.Outcome.Should().Be("interrupt");
        result.Interrupt.Should().NotBeNull();
        result.Interrupt!.Id.Should().Be("call_abc");
        result.Interrupt.Payload!.Value.GetProperty("functionName").GetString().Should().Be("get_weather");
    }

    [Fact]
    public void RunFinishedEvent_Roundtrip_UserInputInterrupt()
    {
        // Arrange - simulates a user input interrupt
        const string originalJson = """
            {
                "type": "RUN_FINISHED",
                "threadId": "thread_123",
                "runId": "run_456",
                "outcome": "interrupt",
                "interrupt": {
                    "id": "input_789",
                    "payload": {
                        "prompt": "Please enter your email address",
                        "inputType": "email",
                        "required": true
                    }
                }
            }
            """;

        // Act
        var deserialized = JsonSerializer.Deserialize<BaseEvent>(originalJson, s_options);
        var reserialized = JsonSerializer.Serialize(deserialized, s_options);
        var final = JsonSerializer.Deserialize<BaseEvent>(reserialized, s_options);

        // Assert
        final.Should().BeOfType<RunFinishedEvent>();
        var result = (RunFinishedEvent)final!;
        result.Outcome.Should().Be("interrupt");
        result.Interrupt.Should().NotBeNull();
        result.Interrupt!.Id.Should().Be("input_789");
        result.Interrupt.Payload!.Value.GetProperty("prompt").GetString().Should().Be("Please enter your email address");
    }
}
