// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Extensions.AI;

#pragma warning disable MEAI001 // Experimental API - FunctionApprovalRequestContent, UserInputRequestContent

namespace Microsoft.Agents.AI.AGUI.UnitTests;

/// <summary>
/// Unit tests for interrupt content type mappings between AG-UI and MEAI.
/// </summary>
public sealed class InterruptMappingTests
{
    private static readonly JsonSerializerOptions s_options = AGUIJsonSerializerContext.Default.Options;

    [Fact]
    public void FromAGUIInterrupt_WithFunctionApprovalPayload_ReturnsFunctionApprovalRequestContent()
    {
        // Arrange
        var payload = JsonDocument.Parse("""
            {
                "functionName": "get_weather",
                "functionArguments": {
                    "location": "Seattle",
                    "units": "fahrenheit"
                }
            }
            """).RootElement;

        var interrupt = new AGUIInterrupt
        {
            Id = "call_abc",
            Payload = payload
        };

        // Act
        var content = InterruptContentExtensions.FromAGUIInterrupt(interrupt);

        // Assert
        Assert.IsType<FunctionApprovalRequestContent>(content);
        var approvalRequest = (FunctionApprovalRequestContent)content;
        Assert.Equal("call_abc", approvalRequest.Id);
        Assert.Equal("get_weather", approvalRequest.FunctionCall.Name);
        Assert.NotNull(approvalRequest.FunctionCall.Arguments);
        Assert.Equal("Seattle", approvalRequest.FunctionCall.Arguments!["location"]?.ToString());
        Assert.Equal(interrupt, approvalRequest.RawRepresentation);
    }

    [Fact]
    public void FromAGUIInterrupt_WithUserInputPayload_ReturnsAGUIUserInputRequestContent()
    {
        // Arrange
        var payload = JsonDocument.Parse("""
            {
                "prompt": "Please enter your email address",
                "inputType": "email"
            }
            """).RootElement;

        var interrupt = new AGUIInterrupt
        {
            Id = "input_789",
            Payload = payload
        };

        // Act
        var content = InterruptContentExtensions.FromAGUIInterrupt(interrupt);

        // Assert
        Assert.IsType<AGUIUserInputRequestContent>(content);
        var inputRequest = (AGUIUserInputRequestContent)content;
        Assert.Equal("input_789", inputRequest.Id);
        Assert.Equal(interrupt, inputRequest.RawRepresentation);
    }

    [Fact]
    public void FromAGUIInterrupt_WithNoPayload_ReturnsAGUIUserInputRequestContent()
    {
        // Arrange
        var interrupt = new AGUIInterrupt
        {
            Id = "interrupt_no_payload"
        };

        // Act
        var content = InterruptContentExtensions.FromAGUIInterrupt(interrupt);

        // Assert
        Assert.IsType<AGUIUserInputRequestContent>(content);
        var inputRequest = (AGUIUserInputRequestContent)content;
        Assert.Equal("interrupt_no_payload", inputRequest.Id);
    }

    [Fact]
    public void ToAGUIInterrupt_FromFunctionApprovalRequest_IncludesFunctionDetailsInPayload()
    {
        // Arrange
        var functionCall = new FunctionCallContent(
            "call_abc",
            "get_weather",
            new Dictionary<string, object?> { ["location"] = "Seattle" });
        var approvalRequest = new FunctionApprovalRequestContent("call_abc", functionCall);

        // Act
        var interrupt = InterruptContentExtensions.ToAGUIInterrupt(approvalRequest, s_options);

        // Assert
        Assert.Equal("call_abc", interrupt.Id);
        Assert.NotNull(interrupt.Payload);
        Assert.Equal("get_weather", interrupt.Payload!.Value.GetProperty("functionName").GetString());
        Assert.Equal("Seattle", interrupt.Payload!.Value.GetProperty("functionArguments").GetProperty("location").GetString());
    }

    [Fact]
    public void ToAGUIInterrupt_FromUserInputRequest_WithRawRepresentation_UsesExistingInterrupt()
    {
        // Arrange
        var existingInterrupt = new AGUIInterrupt
        {
            Id = "input_789",
            Payload = JsonDocument.Parse("""{"prompt":"Enter email"}""").RootElement
        };
        var inputRequest = new AGUIUserInputRequestContent("input_789")
        {
            RawRepresentation = existingInterrupt
        };

        // Act
        var interrupt = InterruptContentExtensions.ToAGUIInterrupt(inputRequest);

        // Assert
        Assert.Same(existingInterrupt, interrupt);
    }

    [Fact]
    public void ToAGUIInterrupt_FromUserInputRequest_WithJsonElement_UsesAsPayload()
    {
        // Arrange
        var jsonPayload = JsonDocument.Parse("""{"custom":"data"}""").RootElement;
        var inputRequest = new AGUIUserInputRequestContent("input_123")
        {
            RawRepresentation = jsonPayload
        };

        // Act
        var interrupt = InterruptContentExtensions.ToAGUIInterrupt(inputRequest);

        // Assert
        Assert.Equal("input_123", interrupt.Id);
        Assert.Equal("data", interrupt.Payload!.Value.GetProperty("custom").GetString());
    }

    [Fact]
    public void ToAGUIResume_FromFunctionApprovalResponse_IncludesApprovalStatus()
    {
        // Arrange
        var functionCall = new FunctionCallContent("call_abc", "test_func", null);
        var approvalResponse = new FunctionApprovalResponseContent("call_abc", true, functionCall);

        // Act
        var resume = InterruptContentExtensions.ToAGUIResume(approvalResponse, s_options);

        // Assert
        Assert.Equal("call_abc", resume.InterruptId);
        Assert.NotNull(resume.Payload);
        Assert.True(resume.Payload!.Value.GetProperty("approved").GetBoolean());
    }

    [Fact]
    public void ToAGUIResume_FromFunctionApprovalResponse_Rejected()
    {
        // Arrange
        var functionCall = new FunctionCallContent("call_abc", "test_func", null);
        var approvalResponse = new FunctionApprovalResponseContent("call_abc", false, functionCall);

        // Act
        var resume = InterruptContentExtensions.ToAGUIResume(approvalResponse, s_options);

        // Assert
        Assert.Equal("call_abc", resume.InterruptId);
        Assert.NotNull(resume.Payload);
        Assert.False(resume.Payload!.Value.GetProperty("approved").GetBoolean());
    }

    [Fact]
    public void ToAGUIResume_FromUserInputResponse_WithJsonElement()
    {
        // Arrange
        var jsonResponse = JsonDocument.Parse("""{"email":"user@example.com","confirmed":true}""").RootElement;
        var inputResponse = new AGUIUserInputResponseContent("input_789")
        {
            RawRepresentation = jsonResponse
        };

        // Act
        var resume = InterruptContentExtensions.ToAGUIResume(inputResponse, s_options);

        // Assert
        Assert.Equal("input_789", resume.InterruptId);
        Assert.NotNull(resume.Payload);
        Assert.Equal("user@example.com", resume.Payload!.Value.GetProperty("email").GetString());
        Assert.True(resume.Payload!.Value.GetProperty("confirmed").GetBoolean());
    }

    [Fact]
    public void FromAGUIResume_WithApprovalResponse_ReturnsFunctionApprovalResponseContent()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"approved":true}""").RootElement;
        var resume = new AGUIResume
        {
            InterruptId = "call_abc",
            Payload = payload
        };
        var originalInterrupt = new FunctionApprovalRequestContent(
            "call_abc",
            new FunctionCallContent("call_abc", "test_func", null));

        // Act
        var content = InterruptContentExtensions.FromAGUIResume(resume, originalInterrupt);

        // Assert
        Assert.IsType<FunctionApprovalResponseContent>(content);
        var approvalResponse = (FunctionApprovalResponseContent)content;
        Assert.Equal("call_abc", approvalResponse.Id);
        Assert.True(approvalResponse.Approved);
        Assert.Equal(resume, approvalResponse.RawRepresentation);
    }

    [Fact]
    public void FromAGUIResume_WithUserInputResponse_ReturnsAGUIUserInputResponseContent()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"response":"user@example.com"}""").RootElement;
        var resume = new AGUIResume
        {
            InterruptId = "input_789",
            Payload = payload
        };
        var originalInterrupt = new AGUIUserInputRequestContent("input_789");

        // Act
        var content = InterruptContentExtensions.FromAGUIResume(resume, originalInterrupt);

        // Assert
        Assert.IsType<AGUIUserInputResponseContent>(content);
        var inputResponse = (AGUIUserInputResponseContent)content;
        Assert.Equal("input_789", inputResponse.Id);
        Assert.Equal(resume, inputResponse.RawRepresentation);
    }

    [Fact]
    public void FromAGUIResume_WithoutOriginalInterrupt_ReturnsAGUIUserInputResponseContent()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"data":"some value"}""").RootElement;
        var resume = new AGUIResume
        {
            InterruptId = "unknown_123",
            Payload = payload
        };

        // Act
        var content = InterruptContentExtensions.FromAGUIResume(resume);

        // Assert
        Assert.IsType<AGUIUserInputResponseContent>(content);
        var inputResponse = (AGUIUserInputResponseContent)content;
        Assert.Equal("unknown_123", inputResponse.Id);
    }

    [Fact]
    public void EndToEnd_FunctionApprovalInterruptCycle()
    {
        // Arrange - Server creates function approval request
        var functionCall = new FunctionCallContent("call_abc", "delete_file", new Dictionary<string, object?> { ["path"] = "/important.txt" });
        var approvalRequest = new FunctionApprovalRequestContent("call_abc", functionCall);

        // Act 1 - Convert to AG-UI interrupt
        var interrupt = InterruptContentExtensions.ToAGUIInterrupt(approvalRequest, s_options);

        // Assert interrupt structure
        Assert.Equal("call_abc", interrupt.Id);
        Assert.Equal("delete_file", interrupt.Payload!.Value.GetProperty("functionName").GetString());

        // Act 2 - Client receives interrupt and converts back to MEAI
        var receivedContent = InterruptContentExtensions.FromAGUIInterrupt(interrupt);
        Assert.IsType<FunctionApprovalRequestContent>(receivedContent);

        // Act 3 - User approves, client creates response
        var approvalResponse = new FunctionApprovalResponseContent("call_abc", true, functionCall);
        var resume = InterruptContentExtensions.ToAGUIResume(approvalResponse, s_options);

        // Act 4 - Server receives resume and converts back to MEAI
        var resumeContent = InterruptContentExtensions.FromAGUIResume(resume, receivedContent);

        // Assert final state
        Assert.IsType<FunctionApprovalResponseContent>(resumeContent);
        var finalResponse = (FunctionApprovalResponseContent)resumeContent;
        Assert.True(finalResponse.Approved);
    }

    [Fact]
    public void EndToEnd_UserInputInterruptCycle()
    {
        // Arrange - Server creates user input request
        var inputRequest = new AGUIUserInputRequestContent("input_789")
        {
            RawRepresentation = JsonDocument.Parse("""{"prompt":"Enter your API key","inputType":"password"}""").RootElement
        };

        // Act 1 - Convert to AG-UI interrupt (via RawRepresentation path)
        var interrupt = InterruptContentExtensions.ToAGUIInterrupt(inputRequest);

        // Assert interrupt structure
        Assert.Equal("input_789", interrupt.Id);

        // Act 2 - Client receives interrupt and converts back to MEAI
        var receivedContent = InterruptContentExtensions.FromAGUIInterrupt(interrupt);
        Assert.IsAssignableFrom<UserInputRequestContent>(receivedContent);

        // Act 3 - User provides input, client creates response
        var inputResponse = new AGUIUserInputResponseContent("input_789")
        {
            RawRepresentation = JsonDocument.Parse("""{"response":"sk-secret-key-123"}""").RootElement
        };
        var resume = InterruptContentExtensions.ToAGUIResume(inputResponse, s_options);

        // Act 4 - Server receives resume and converts back to MEAI
        var resumeContent = InterruptContentExtensions.FromAGUIResume(resume, receivedContent);

        // Assert final state
        Assert.IsAssignableFrom<UserInputResponseContent>(resumeContent);
        Assert.Equal("input_789", ((UserInputResponseContent)resumeContent).Id);
    }
}
