// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AGUI.Protocol.UnitTests;

public class MultimodalContentTests
{
    private static readonly JsonSerializerOptions s_options = AGUIJsonSerializerContext.Default.Options;

    [Fact]
    public void AGUITextInputContent_SerializesCorrectly()
    {
        // Arrange
        var content = new AGUITextInputContent
        {
            Text = "Hello, world!"
        };

        // Act
        var json = JsonSerializer.Serialize(content, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIInputContent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUITextInputContent>();
        var result = (AGUITextInputContent)deserialized!;
        result.Type.Should().Be(AGUIInputContentTypes.Text);
        result.Text.Should().Be("Hello, world!");
    }

    [Fact]
    public void AGUIBinaryInputContent_WithData_SerializesCorrectly()
    {
        // Arrange
        var content = new AGUIBinaryInputContent
        {
            MimeType = "image/jpeg",
            Data = "base64encodeddata",
            Filename = "photo.jpg"
        };

        // Act
        var json = JsonSerializer.Serialize(content, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIInputContent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUIBinaryInputContent>();
        var result = (AGUIBinaryInputContent)deserialized!;
        result.Type.Should().Be(AGUIInputContentTypes.Binary);
        result.MimeType.Should().Be("image/jpeg");
        result.Data.Should().Be("base64encodeddata");
        result.Filename.Should().Be("photo.jpg");
        result.Url.Should().BeNull();
        result.Id.Should().BeNull();
    }

    [Fact]
    public void AGUIBinaryInputContent_WithUrl_SerializesCorrectly()
    {
        // Arrange
        var content = new AGUIBinaryInputContent
        {
            MimeType = "image/png",
            Url = "https://example.com/image.png"
        };

        // Act
        var json = JsonSerializer.Serialize(content, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIInputContent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUIBinaryInputContent>();
        var result = (AGUIBinaryInputContent)deserialized!;
        result.Type.Should().Be(AGUIInputContentTypes.Binary);
        result.MimeType.Should().Be("image/png");
        result.Url.Should().Be("https://example.com/image.png");
        result.Data.Should().BeNull();
    }

    [Fact]
    public void AGUIBinaryInputContent_WithId_SerializesCorrectly()
    {
        // Arrange
        var content = new AGUIBinaryInputContent
        {
            MimeType = "audio/wav",
            Id = "upload-123",
            Filename = "recording.wav"
        };

        // Act
        var json = JsonSerializer.Serialize(content, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIInputContent>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUIBinaryInputContent>();
        var result = (AGUIBinaryInputContent)deserialized!;
        result.Type.Should().Be(AGUIInputContentTypes.Binary);
        result.MimeType.Should().Be("audio/wav");
        result.Id.Should().Be("upload-123");
        result.Filename.Should().Be("recording.wav");
    }

    [Fact]
    public void AGUIUserMessage_StringContent_DeserializesToTextInputContent()
    {
        // Arrange - simulates wire format where content is just a string
        const string json = """{"id":"msg_123","role":"user","content":"Hello, world!"}""";

        // Act
        var deserialized = JsonSerializer.Deserialize<AGUIMessage>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUIUserMessage>();
        var result = (AGUIUserMessage)deserialized!;
        result.Id.Should().Be("msg_123");
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<AGUITextInputContent>();
        ((AGUITextInputContent)result.Content[0]).Text.Should().Be("Hello, world!");
    }

    [Fact]
    public void AGUIUserMessage_MultimodalContent_DeserializesCorrectly()
    {
        // Arrange - simulates wire format with array content
        const string json = """
            {
                "id": "msg_456",
                "role": "user",
                "content": [
                    { "type": "text", "text": "What's in this image?" },
                    { "type": "binary", "mimeType": "image/jpeg", "data": "base64data" }
                ]
            }
            """;

        // Act
        var deserialized = JsonSerializer.Deserialize<AGUIMessage>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUIUserMessage>();
        var result = (AGUIUserMessage)deserialized!;
        result.Id.Should().Be("msg_456");
        result.Content.Should().HaveCount(2);

        result.Content[0].Should().BeOfType<AGUITextInputContent>();
        ((AGUITextInputContent)result.Content[0]).Text.Should().Be("What's in this image?");

        result.Content[1].Should().BeOfType<AGUIBinaryInputContent>();
        var binary = (AGUIBinaryInputContent)result.Content[1];
        binary.MimeType.Should().Be("image/jpeg");
        binary.Data.Should().Be("base64data");
    }

    [Fact]
    public void AGUIUserMessage_SingleTextContent_SerializesAsString()
    {
        // Arrange
        var message = new AGUIUserMessage
        {
            Id = "msg_789",
            Content = new List<AGUIInputContent>
            {
                new AGUITextInputContent { Text = "Hello!" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize<AGUIMessage>(message, s_options);

        // Assert - should be serialized as string for wire compatibility
        json.Should().Contain("\"content\":\"Hello!\"");
        json.Should().NotContain("\"type\":\"text\"");
    }

    [Fact]
    public void AGUIUserMessage_MultipleContents_SerializesAsArray()
    {
        // Arrange
        var message = new AGUIUserMessage
        {
            Id = "msg_abc",
            Content = new List<AGUIInputContent>
            {
                new AGUITextInputContent { Text = "Describe this:" },
                new AGUIBinaryInputContent { MimeType = "image/png", Url = "https://example.com/img.png" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize<AGUIMessage>(message, s_options);

        // Assert - should be serialized as array
        json.Should().Contain("\"content\":[");
        json.Should().Contain("\"type\":\"text\"");
        json.Should().Contain("\"type\":\"binary\"");
    }

    [Fact]
    public void AGUIUserMessage_EmptyContent_SerializesAsEmptyString()
    {
        // Arrange
        var message = new AGUIUserMessage
        {
            Id = "msg_empty",
            Content = new List<AGUIInputContent>()
        };

        // Act
        var json = JsonSerializer.Serialize<AGUIMessage>(message, s_options);

        // Assert
        json.Should().Contain("\"content\":\"\"");
    }

    [Fact]
    public void AGUIUserMessage_RoundTrip_PreservesMultimodalContent()
    {
        // Arrange
        var original = new AGUIUserMessage
        {
            Id = "msg_roundtrip",
            Name = "User1",
            Content = new List<AGUIInputContent>
            {
                new AGUITextInputContent { Text = "Check these images" },
                new AGUIBinaryInputContent
                {
                    MimeType = "image/jpeg",
                    Data = "base64data1",
                    Filename = "image1.jpg"
                },
                new AGUIBinaryInputContent
                {
                    MimeType = "image/png",
                    Url = "https://example.com/image2.png"
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize<AGUIMessage>(original, s_options);
        var deserialized = JsonSerializer.Deserialize<AGUIMessage>(json, s_options);

        // Assert
        deserialized.Should().BeOfType<AGUIUserMessage>();
        var result = (AGUIUserMessage)deserialized!;
        result.Id.Should().Be("msg_roundtrip");
        result.Name.Should().Be("User1");
        result.Content.Should().HaveCount(3);

        result.Content[0].Should().BeOfType<AGUITextInputContent>();
        ((AGUITextInputContent)result.Content[0]).Text.Should().Be("Check these images");

        result.Content[1].Should().BeOfType<AGUIBinaryInputContent>();
        var binary1 = (AGUIBinaryInputContent)result.Content[1];
        binary1.MimeType.Should().Be("image/jpeg");
        binary1.Data.Should().Be("base64data1");
        binary1.Filename.Should().Be("image1.jpg");

        result.Content[2].Should().BeOfType<AGUIBinaryInputContent>();
        var binary2 = (AGUIBinaryInputContent)result.Content[2];
        binary2.MimeType.Should().Be("image/png");
        binary2.Url.Should().Be("https://example.com/image2.png");
    }

    [Fact]
    public void AGUIUserMessage_NullValues_OmittedFromJson()
    {
        // Arrange
        var content = new AGUIBinaryInputContent
        {
            MimeType = "image/jpeg",
            Data = "base64data"
            // Url, Id, Filename are null
        };

        // Act
        var json = JsonSerializer.Serialize(content, s_options);

        // Assert
        json.Should().NotContain("\"url\"");
        json.Should().NotContain("\"id\"");
        json.Should().NotContain("\"filename\"");
    }
}
