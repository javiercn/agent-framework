// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AGUI.Protocol.UnitTests;

public class MessageSerializationTests
{
    private static readonly JsonSerializerOptions s_options = AGUIJsonSerializerContext.Default.Options;

    [Fact]
    public void AGUIUserMessage_SerializesCorrectly()
    {
        // Arrange
        var msg = new AGUIUserMessage
        {
            Id = "msg_123",
            Content = "Hello!",
            Name = "John"
        };

        // Act
        var json = JsonSerializer.Serialize(msg, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIMessage>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUIUserMessage>();
        var result = (AGUIUserMessage)deserialized!;
        result.Role.Should().Be(AGUIRoles.User);
        result.Content.Should().Be("Hello!");
        result.Name.Should().Be("John");
    }

    [Fact]
    public void AGUIAssistantMessage_SerializesCorrectly()
    {
        // Arrange
        var msg = new AGUIAssistantMessage
        {
            Id = "msg_123",
            Content = "Hello back!",
            ToolCalls =
            [
                new AGUIToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new AGUIFunctionCall
                    {
                        Name = "get_weather",
                        Arguments = "{\"city\": \"Seattle\"}"
                    }
                }
            ]
        };

        // Act
        var json = JsonSerializer.Serialize(msg, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIMessage>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUIAssistantMessage>();
        var result = (AGUIAssistantMessage)deserialized!;
        result.Role.Should().Be(AGUIRoles.Assistant);
        result.ToolCalls.Should().HaveCount(1);
        result.ToolCalls![0].Function.Name.Should().Be("get_weather");
    }

    [Fact]
    public void AGUIToolMessage_SerializesCorrectly()
    {
        // Arrange
        var msg = new AGUIToolMessage
        {
            Id = "msg_456",
            Content = "72°F",
            ToolCallId = "call_1"
        };

        // Act
        var json = JsonSerializer.Serialize(msg, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIMessage>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUIToolMessage>();
        var result = (AGUIToolMessage)deserialized!;
        result.Role.Should().Be(AGUIRoles.Tool);
        result.Content.Should().Be("72°F");
        result.ToolCallId.Should().Be("call_1");
    }

    [Fact]
    public void AGUISystemMessage_SerializesCorrectly()
    {
        // Arrange
        var msg = new AGUISystemMessage
        {
            Content = "You are a helpful assistant."
        };

        // Act
        var json = JsonSerializer.Serialize(msg, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIMessage>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUISystemMessage>();
        var result = (AGUISystemMessage)deserialized!;
        result.Role.Should().Be(AGUIRoles.System);
        result.Content.Should().Be("You are a helpful assistant.");
    }

    [Fact]
    public void AGUIDeveloperMessage_SerializesCorrectly()
    {
        // Arrange
        var msg = new AGUIDeveloperMessage
        {
            Content = "Internal instructions"
        };

        // Act
        var json = JsonSerializer.Serialize(msg, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIMessage>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUIDeveloperMessage>();
        var result = (AGUIDeveloperMessage)deserialized!;
        result.Role.Should().Be(AGUIRoles.Developer);
    }

    [Fact]
    public void AGUIMessage_NullId_OmittedFromJson()
    {
        // Arrange
        var msg = new AGUIUserMessage
        {
            Id = null,
            Content = "Hello!"
        };

        // Act
        var json = JsonSerializer.Serialize(msg, s_options);

        // Assert
        json.Should().NotContain("\"id\"");
    }

    [Fact]
    public void AGUIAssistantMessage_NullToolCalls_OmittedFromJson()
    {
        // Arrange
        var msg = new AGUIAssistantMessage
        {
            Content = "Hello!",
            ToolCalls = null
        };

        // Act
        var json = JsonSerializer.Serialize(msg, s_options);

        // Assert
        json.Should().NotContain("toolCalls");
    }
}
