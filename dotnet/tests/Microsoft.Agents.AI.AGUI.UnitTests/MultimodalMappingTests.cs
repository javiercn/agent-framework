// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using AGUI.Protocol;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.AGUI.UnitTests;

/// <summary>
/// Unit tests for multimodal content mapping between AG-UI and M.E.AI types.
/// Tests the bidirectional conversion in <see cref="AGUIChatMessageExtensions"/>.
/// </summary>
public sealed class MultimodalMappingTests
{
    #region AG-UI to M.E.AI (AsChatMessages) Tests

    [Fact]
    public void AsChatMessages_UserWithTextInputContent_MapsToTextContent()
    {
        // Arrange
        List<AGUIMessage> aguiMessages =
        [
            new AGUIUserMessage
            {
                Id = "msg1",
                Content = [new AGUITextInputContent { Text = "Hello, world!" }]
            }
        ];

        // Act
        List<ChatMessage> chatMessages = aguiMessages.AsChatMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        ChatMessage message = Assert.Single(chatMessages);
        Assert.Equal(ChatRole.User, message.Role);
        Assert.Equal("msg1", message.MessageId);
        AIContent content = Assert.Single(message.Contents);
        TextContent textContent = Assert.IsType<TextContent>(content);
        Assert.Equal("Hello, world!", textContent.Text);
    }

    [Fact]
    public void AsChatMessages_UserWithBinaryInputContent_Data_MapsToDataContent()
    {
        // Arrange
        byte[] expectedBytes = [1, 2, 3, 4, 5];
        string base64Data = Convert.ToBase64String(expectedBytes);

        List<AGUIMessage> aguiMessages =
        [
            new AGUIUserMessage
            {
                Id = "msg1",
                Content =
                [
                    new AGUIBinaryInputContent
                    {
                        MimeType = "image/jpeg",
                        Data = base64Data,
                        Filename = "photo.jpg",
                        Id = "upload-123"
                    }
                ]
            }
        ];

        // Act
        List<ChatMessage> chatMessages = aguiMessages.AsChatMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        ChatMessage message = Assert.Single(chatMessages);
        AIContent content = Assert.Single(message.Contents);
        DataContent dataContent = Assert.IsType<DataContent>(content);

        Assert.Equal("image/jpeg", dataContent.MediaType);
        Assert.Equal(expectedBytes, dataContent.Data.ToArray());
        Assert.NotNull(dataContent.AdditionalProperties);
        Assert.Equal("photo.jpg", dataContent.AdditionalProperties["filename"]);
        Assert.Equal("upload-123", dataContent.AdditionalProperties["ag_ui_content_id"]);
        Assert.IsType<AGUIBinaryInputContent>(dataContent.RawRepresentation);
    }

    [Fact]
    public void AsChatMessages_UserWithBinaryInputContent_Url_MapsToUriContent()
    {
        // Arrange
        List<AGUIMessage> aguiMessages =
        [
            new AGUIUserMessage
            {
                Id = "msg1",
                Content =
                [
                    new AGUIBinaryInputContent
                    {
                        MimeType = "image/png",
                        Url = "https://example.com/image.png",
                        Filename = "image.png",
                        Id = "ref-456"
                    }
                ]
            }
        ];

        // Act
        List<ChatMessage> chatMessages = aguiMessages.AsChatMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        ChatMessage message = Assert.Single(chatMessages);
        AIContent content = Assert.Single(message.Contents);
        UriContent uriContent = Assert.IsType<UriContent>(content);

        Assert.Equal("image/png", uriContent.MediaType);
        Assert.Equal(new Uri("https://example.com/image.png"), uriContent.Uri);
        Assert.NotNull(uriContent.AdditionalProperties);
        Assert.Equal("image.png", uriContent.AdditionalProperties["filename"]);
        Assert.Equal("ref-456", uriContent.AdditionalProperties["ag_ui_content_id"]);
        Assert.IsType<AGUIBinaryInputContent>(uriContent.RawRepresentation);
    }

    [Fact]
    public void AsChatMessages_UserWithBinaryInputContent_IdOnly_MapsToDataContentWithMarker()
    {
        // Arrange
        List<AGUIMessage> aguiMessages =
        [
            new AGUIUserMessage
            {
                Id = "msg1",
                Content =
                [
                    new AGUIBinaryInputContent
                    {
                        MimeType = "audio/wav",
                        Id = "upload-789",
                        Filename = "recording.wav"
                    }
                ]
            }
        ];

        // Act
        List<ChatMessage> chatMessages = aguiMessages.AsChatMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        ChatMessage message = Assert.Single(chatMessages);
        AIContent content = Assert.Single(message.Contents);
        DataContent dataContent = Assert.IsType<DataContent>(content);

        Assert.Equal("audio/wav", dataContent.MediaType);
        Assert.True(dataContent.Data.IsEmpty); // No actual data
        Assert.NotNull(dataContent.AdditionalProperties);
        Assert.Equal("upload-789", dataContent.AdditionalProperties["ag_ui_content_id"]);
        Assert.Equal(true, dataContent.AdditionalProperties["ag_ui_requires_resolution"]);
        Assert.Equal("recording.wav", dataContent.AdditionalProperties["filename"]);
    }

    [Fact]
    public void AsChatMessages_UserWithMultimodalContent_MapsAllContents()
    {
        // Arrange
        byte[] imageBytes = [10, 20, 30];
        string base64Data = Convert.ToBase64String(imageBytes);

        List<AGUIMessage> aguiMessages =
        [
            new AGUIUserMessage
            {
                Id = "msg1",
                Content =
                [
                    new AGUITextInputContent { Text = "What's in this image?" },
                    new AGUIBinaryInputContent { MimeType = "image/jpeg", Data = base64Data },
                    new AGUIBinaryInputContent { MimeType = "image/png", Url = "https://example.com/other.png" }
                ]
            }
        ];

        // Act
        List<ChatMessage> chatMessages = aguiMessages.AsChatMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        ChatMessage message = Assert.Single(chatMessages);
        Assert.Equal(3, message.Contents.Count);

        TextContent textContent = Assert.IsType<TextContent>(message.Contents[0]);
        Assert.Equal("What's in this image?", textContent.Text);

        DataContent dataContent = Assert.IsType<DataContent>(message.Contents[1]);
        Assert.Equal("image/jpeg", dataContent.MediaType);
        Assert.Equal(imageBytes, dataContent.Data.ToArray());

        UriContent uriContent = Assert.IsType<UriContent>(message.Contents[2]);
        Assert.Equal("image/png", uriContent.MediaType);
        Assert.Equal(new Uri("https://example.com/other.png"), uriContent.Uri);
    }

    [Fact]
    public void AsChatMessages_UserWithBinaryInputContent_NoDataUrlOrId_ThrowsException()
    {
        // Arrange
        List<AGUIMessage> aguiMessages =
        [
            new AGUIUserMessage
            {
                Id = "msg1",
                Content =
                [
                    new AGUIBinaryInputContent
                    {
                        MimeType = "application/pdf"
                        // No Data, Url, or Id
                    }
                ]
            }
        ];

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            aguiMessages.AsChatMessages(AGUIJsonSerializerContext.Default.Options).ToList());
    }

    #endregion

    #region M.E.AI to AG-UI (AsAGUIMessages) Tests

    [Fact]
    public void AsAGUIMessages_UserWithTextContent_MapsToTextInputContent()
    {
        // Arrange
        List<ChatMessage> chatMessages =
        [
            new ChatMessage(ChatRole.User, [new TextContent("Hello!")]) { MessageId = "msg1" }
        ];

        // Act
        List<AGUIMessage> aguiMessages = chatMessages.AsAGUIMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        AGUIMessage message = Assert.Single(aguiMessages);
        AGUIUserMessage userMessage = Assert.IsType<AGUIUserMessage>(message);
        Assert.Equal("msg1", userMessage.Id);
        AGUIInputContent content = Assert.Single(userMessage.Content);
        AGUITextInputContent textContent = Assert.IsType<AGUITextInputContent>(content);
        Assert.Equal("Hello!", textContent.Text);
    }

    [Fact]
    public void AsAGUIMessages_UserWithDataContent_MapsToBinaryInputContent()
    {
        // Arrange
        byte[] imageBytes = [1, 2, 3, 4, 5];
        DataContent dataContent = new(imageBytes, "image/jpeg");
        dataContent.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            ["filename"] = "photo.jpg",
            ["ag_ui_content_id"] = "upload-123"
        };

        List<ChatMessage> chatMessages =
        [
            new ChatMessage(ChatRole.User, [dataContent]) { MessageId = "msg1" }
        ];

        // Act
        List<AGUIMessage> aguiMessages = chatMessages.AsAGUIMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        AGUIMessage message = Assert.Single(aguiMessages);
        AGUIUserMessage userMessage = Assert.IsType<AGUIUserMessage>(message);
        AGUIInputContent content = Assert.Single(userMessage.Content);
        AGUIBinaryInputContent binaryContent = Assert.IsType<AGUIBinaryInputContent>(content);

        Assert.Equal("image/jpeg", binaryContent.MimeType);
        Assert.Equal(Convert.ToBase64String(imageBytes), binaryContent.Data);
        Assert.Equal("photo.jpg", binaryContent.Filename);
        Assert.Equal("upload-123", binaryContent.Id);
    }

    [Fact]
    public void AsAGUIMessages_UserWithUriContent_MapsToBinaryInputContent()
    {
        // Arrange
        UriContent uriContent = new(new Uri("https://example.com/image.png"), "image/png");
        uriContent.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            ["filename"] = "image.png",
            ["ag_ui_content_id"] = "ref-456"
        };

        List<ChatMessage> chatMessages =
        [
            new ChatMessage(ChatRole.User, [uriContent]) { MessageId = "msg1" }
        ];

        // Act
        List<AGUIMessage> aguiMessages = chatMessages.AsAGUIMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        AGUIMessage message = Assert.Single(aguiMessages);
        AGUIUserMessage userMessage = Assert.IsType<AGUIUserMessage>(message);
        AGUIInputContent content = Assert.Single(userMessage.Content);
        AGUIBinaryInputContent binaryContent = Assert.IsType<AGUIBinaryInputContent>(content);

        Assert.Equal("image/png", binaryContent.MimeType);
        Assert.Equal("https://example.com/image.png", binaryContent.Url);
        Assert.Equal("image.png", binaryContent.Filename);
        Assert.Equal("ref-456", binaryContent.Id);
        Assert.Null(binaryContent.Data); // URL content, no inline data
    }

    [Fact]
    public void AsAGUIMessages_UserWithMultipleContents_MapsAllContents()
    {
        // Arrange
        byte[] imageBytes = [10, 20, 30];
        List<ChatMessage> chatMessages =
        [
            new ChatMessage(ChatRole.User,
            [
                new TextContent("Analyze these:"),
                new DataContent(imageBytes, "image/jpeg"),
                new UriContent(new Uri("https://example.com/doc.pdf"), "application/pdf")
            ])
            { MessageId = "msg1" }
        ];

        // Act
        List<AGUIMessage> aguiMessages = chatMessages.AsAGUIMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        AGUIMessage message = Assert.Single(aguiMessages);
        AGUIUserMessage userMessage = Assert.IsType<AGUIUserMessage>(message);
        Assert.Equal(3, userMessage.Content.Count);

        AGUITextInputContent text = Assert.IsType<AGUITextInputContent>(userMessage.Content[0]);
        Assert.Equal("Analyze these:", text.Text);

        AGUIBinaryInputContent binary1 = Assert.IsType<AGUIBinaryInputContent>(userMessage.Content[1]);
        Assert.Equal("image/jpeg", binary1.MimeType);
        Assert.Equal(Convert.ToBase64String(imageBytes), binary1.Data);

        AGUIBinaryInputContent binary2 = Assert.IsType<AGUIBinaryInputContent>(userMessage.Content[2]);
        Assert.Equal("application/pdf", binary2.MimeType);
        Assert.Equal("https://example.com/doc.pdf", binary2.Url);
    }

    [Fact]
    public void AsAGUIMessages_UserWithEmptyDataContent_MapsWithNoData()
    {
        // Arrange
        DataContent dataContent = new(ReadOnlyMemory<byte>.Empty, "application/octet-stream");

        List<ChatMessage> chatMessages =
        [
            new ChatMessage(ChatRole.User, [dataContent]) { MessageId = "msg1" }
        ];

        // Act
        List<AGUIMessage> aguiMessages = chatMessages.AsAGUIMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        AGUIMessage message = Assert.Single(aguiMessages);
        AGUIUserMessage userMessage = Assert.IsType<AGUIUserMessage>(message);
        AGUIInputContent content = Assert.Single(userMessage.Content);
        AGUIBinaryInputContent binaryContent = Assert.IsType<AGUIBinaryInputContent>(content);

        Assert.Equal("application/octet-stream", binaryContent.MimeType);
        Assert.Null(binaryContent.Data); // Empty data should result in null
    }

    [Fact]
    public void AsAGUIMessages_UserWithTextString_FallsBackToTextInputContent()
    {
        // Arrange - Using the string constructor for ChatMessage
        List<ChatMessage> chatMessages =
        [
            new ChatMessage(ChatRole.User, "Simple text message") { MessageId = "msg1" }
        ];

        // Act
        List<AGUIMessage> aguiMessages = chatMessages.AsAGUIMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        AGUIMessage message = Assert.Single(aguiMessages);
        AGUIUserMessage userMessage = Assert.IsType<AGUIUserMessage>(message);
        AGUIInputContent content = Assert.Single(userMessage.Content);
        AGUITextInputContent textContent = Assert.IsType<AGUITextInputContent>(content);
        Assert.Equal("Simple text message", textContent.Text);
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void RoundTrip_UserWithTextContent_PreservesData()
    {
        // Arrange
        List<AGUIMessage> original =
        [
            new AGUIUserMessage
            {
                Id = "msg1",
                Content = [new AGUITextInputContent { Text = "Hello!" }]
            }
        ];

        // Act - AG-UI -> MEAI -> AG-UI
        List<ChatMessage> meaiMessages = original.AsChatMessages(AGUIJsonSerializerContext.Default.Options).ToList();
        List<AGUIMessage> roundTripped = meaiMessages.AsAGUIMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        AGUIMessage message = Assert.Single(roundTripped);
        AGUIUserMessage userMessage = Assert.IsType<AGUIUserMessage>(message);
        AGUIInputContent content = Assert.Single(userMessage.Content);
        AGUITextInputContent textContent = Assert.IsType<AGUITextInputContent>(content);
        Assert.Equal("Hello!", textContent.Text);
    }

    [Fact]
    public void RoundTrip_UserWithBinaryDataContent_PreservesData()
    {
        // Arrange
        byte[] imageBytes = [1, 2, 3, 4, 5];
        string base64Data = Convert.ToBase64String(imageBytes);

        List<AGUIMessage> original =
        [
            new AGUIUserMessage
            {
                Id = "msg1",
                Content =
                [
                    new AGUIBinaryInputContent
                    {
                        MimeType = "image/jpeg",
                        Data = base64Data,
                        Filename = "photo.jpg"
                    }
                ]
            }
        ];

        // Act - AG-UI -> MEAI -> AG-UI
        List<ChatMessage> meaiMessages = original.AsChatMessages(AGUIJsonSerializerContext.Default.Options).ToList();
        List<AGUIMessage> roundTripped = meaiMessages.AsAGUIMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        AGUIMessage message = Assert.Single(roundTripped);
        AGUIUserMessage userMessage = Assert.IsType<AGUIUserMessage>(message);
        AGUIInputContent content = Assert.Single(userMessage.Content);
        AGUIBinaryInputContent binaryContent = Assert.IsType<AGUIBinaryInputContent>(content);

        Assert.Equal("image/jpeg", binaryContent.MimeType);
        Assert.Equal(base64Data, binaryContent.Data);
        Assert.Equal("photo.jpg", binaryContent.Filename);
    }

    [Fact]
    public void RoundTrip_UserWithBinaryUrlContent_PreservesData()
    {
        // Arrange
        List<AGUIMessage> original =
        [
            new AGUIUserMessage
            {
                Id = "msg1",
                Content =
                [
                    new AGUIBinaryInputContent
                    {
                        MimeType = "image/png",
                        Url = "https://example.com/image.png",
                        Filename = "image.png"
                    }
                ]
            }
        ];

        // Act - AG-UI -> MEAI -> AG-UI
        List<ChatMessage> meaiMessages = original.AsChatMessages(AGUIJsonSerializerContext.Default.Options).ToList();
        List<AGUIMessage> roundTripped = meaiMessages.AsAGUIMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        AGUIMessage message = Assert.Single(roundTripped);
        AGUIUserMessage userMessage = Assert.IsType<AGUIUserMessage>(message);
        AGUIInputContent content = Assert.Single(userMessage.Content);
        AGUIBinaryInputContent binaryContent = Assert.IsType<AGUIBinaryInputContent>(content);

        Assert.Equal("image/png", binaryContent.MimeType);
        Assert.Equal("https://example.com/image.png", binaryContent.Url);
        Assert.Equal("image.png", binaryContent.Filename);
    }

    [Fact]
    public void RoundTrip_UserWithMultimodalContent_PreservesAllData()
    {
        // Arrange
        byte[] imageBytes = [10, 20, 30];
        string base64Data = Convert.ToBase64String(imageBytes);

        List<AGUIMessage> original =
        [
            new AGUIUserMessage
            {
                Id = "msg1",
                Content =
                [
                    new AGUITextInputContent { Text = "What's in these?" },
                    new AGUIBinaryInputContent
                    {
                        MimeType = "image/jpeg",
                        Data = base64Data,
                        Filename = "photo1.jpg"
                    },
                    new AGUIBinaryInputContent
                    {
                        MimeType = "image/png",
                        Url = "https://example.com/photo2.png",
                        Filename = "photo2.png"
                    }
                ]
            }
        ];

        // Act - AG-UI -> MEAI -> AG-UI
        List<ChatMessage> meaiMessages = original.AsChatMessages(AGUIJsonSerializerContext.Default.Options).ToList();
        List<AGUIMessage> roundTripped = meaiMessages.AsAGUIMessages(AGUIJsonSerializerContext.Default.Options).ToList();

        // Assert
        AGUIMessage message = Assert.Single(roundTripped);
        AGUIUserMessage userMessage = Assert.IsType<AGUIUserMessage>(message);
        Assert.Equal(3, userMessage.Content.Count);

        AGUITextInputContent text = Assert.IsType<AGUITextInputContent>(userMessage.Content[0]);
        Assert.Equal("What's in these?", text.Text);

        AGUIBinaryInputContent binary1 = Assert.IsType<AGUIBinaryInputContent>(userMessage.Content[1]);
        Assert.Equal("image/jpeg", binary1.MimeType);
        Assert.Equal(base64Data, binary1.Data);
        Assert.Equal("photo1.jpg", binary1.Filename);

        AGUIBinaryInputContent binary2 = Assert.IsType<AGUIBinaryInputContent>(userMessage.Content[2]);
        Assert.Equal("image/png", binary2.MimeType);
        Assert.Equal("https://example.com/photo2.png", binary2.Url);
        Assert.Equal("photo2.png", binary2.Filename);
    }

    #endregion
}
