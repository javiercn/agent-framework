// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.Extensions;

internal static class ServerAGUIChatMessageExtensions
{
    private static readonly ChatRole s_developerChatRole = new("developer");

    public static IEnumerable<ChatMessage> AsChatMessages(
        this IEnumerable<AGUIMessage> aguiMessages,
        JsonSerializerOptions jsonSerializerOptions)
    {
        foreach (var message in aguiMessages)
        {
            var role = MapChatRole(message.Role);

            switch (message)
            {
                case AGUIToolMessage toolMessage:
                {
                    object? result;
                    if (string.IsNullOrEmpty(toolMessage.Content))
                    {
                        result = toolMessage.Content;
                    }
                    else
                    {
                        // Try to deserialize as JSON, but fall back to string if it fails
                        try
                        {
                            result = JsonSerializer.Deserialize(toolMessage.Content, AGUIJsonSerializerContext.Default.JsonElement);
                        }
                        catch (JsonException)
                        {
                            result = toolMessage.Content;
                        }
                    }

                    yield return new ChatMessage(
                        role,
                        [
                            new FunctionResultContent(
                                    toolMessage.ToolCallId,
                                    result)
                        ]);
                    break;
                }

                case AGUIAssistantMessage assistantMessage when assistantMessage.ToolCalls is { Count: > 0 }:
                {
                    var contents = new List<AIContent>();

                    if (!string.IsNullOrEmpty(assistantMessage.Content))
                    {
                        contents.Add(new TextContent(assistantMessage.Content));
                    }

                    // Add tool calls
                    foreach (var toolCall in assistantMessage.ToolCalls)
                    {
                        Dictionary<string, object?>? arguments = null;
                        if (!string.IsNullOrEmpty(toolCall.Function.Arguments))
                        {
                            arguments = (Dictionary<string, object?>?)JsonSerializer.Deserialize(
                                toolCall.Function.Arguments,
                                jsonSerializerOptions.GetTypeInfo(typeof(Dictionary<string, object?>)));
                        }

                        contents.Add(new FunctionCallContent(
                            toolCall.Id,
                            toolCall.Function.Name,
                            arguments));
                    }

                    yield return new ChatMessage(role, contents)
                    {
                        MessageId = message.Id
                    };
                    break;
                }

                case AGUIUserMessage userMessage:
                {
                    // Handle multimodal content for user messages
                    var contents = MapInputContentsToAIContents(userMessage.Content);
                    yield return new ChatMessage(role, contents)
                    {
                        MessageId = message.Id
                    };
                    break;
                }

                default:
                {
                    string content = message switch
                    {
                        AGUIDeveloperMessage dev => dev.Content,
                        AGUISystemMessage sys => sys.Content,
                        AGUIAssistantMessage asst => asst.Content,
                        _ => string.Empty
                    };

                    yield return new ChatMessage(role, content)
                    {
                        MessageId = message.Id
                    };
                    break;
                }
            }
        }
    }

    private static List<AIContent> MapInputContentsToAIContents(IList<AGUIInputContent> inputContents)
    {
        var contents = new List<AIContent>();

        foreach (var inputContent in inputContents)
        {
            switch (inputContent)
            {
                case AGUITextInputContent textContent:
                    contents.Add(new TextContent(textContent.Text));
                    break;

                case AGUIBinaryInputContent binaryContent:
                    contents.Add(MapBinaryContentToAIContent(binaryContent));
                    break;
            }
        }

        return contents;
    }

    private static AIContent MapBinaryContentToAIContent(AGUIBinaryInputContent binaryContent)
    {
        // Priority: data > url > id
        if (!string.IsNullOrEmpty(binaryContent.Data))
        {
            // Inline base64 data -> DataContent
            var bytes = Convert.FromBase64String(binaryContent.Data);
            var dataContent = new DataContent(bytes, binaryContent.MimeType);

            // Store original AG-UI content for reference
            dataContent.AdditionalProperties ??= [];
            if (binaryContent.Filename is not null)
            {
                dataContent.AdditionalProperties["filename"] = binaryContent.Filename;
            }
            if (binaryContent.Id is not null)
            {
                dataContent.AdditionalProperties["ag_ui_content_id"] = binaryContent.Id;
            }
            dataContent.RawRepresentation = binaryContent;

            return dataContent;
        }
        else if (!string.IsNullOrEmpty(binaryContent.Url))
        {
            // URL reference -> UriContent
            var uriContent = new UriContent(new Uri(binaryContent.Url), binaryContent.MimeType);

            uriContent.AdditionalProperties ??= [];
            if (binaryContent.Filename is not null)
            {
                uriContent.AdditionalProperties["filename"] = binaryContent.Filename;
            }
            if (binaryContent.Id is not null)
            {
                uriContent.AdditionalProperties["ag_ui_content_id"] = binaryContent.Id;
            }
            uriContent.RawRepresentation = binaryContent;

            return uriContent;
        }
        else if (!string.IsNullOrEmpty(binaryContent.Id))
        {
            // ID reference only - use DataContent with special handling
            var placeholder = new DataContent(
                ReadOnlyMemory<byte>.Empty,
                binaryContent.MimeType);

            placeholder.AdditionalProperties ??= [];
            placeholder.AdditionalProperties["ag_ui_content_id"] = binaryContent.Id;
            placeholder.AdditionalProperties["ag_ui_requires_resolution"] = true;
            if (binaryContent.Filename is not null)
            {
                placeholder.AdditionalProperties["filename"] = binaryContent.Filename;
            }
            placeholder.RawRepresentation = binaryContent;

            return placeholder;
        }

        throw new InvalidOperationException(
            "BinaryInputContent must have at least one of 'data', 'url', or 'id' specified.");
    }

    public static ChatRole MapChatRole(string role) =>
        string.Equals(role, AGUIRoles.System, StringComparison.OrdinalIgnoreCase) ? ChatRole.System :
        string.Equals(role, AGUIRoles.User, StringComparison.OrdinalIgnoreCase) ? ChatRole.User :
        string.Equals(role, AGUIRoles.Assistant, StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant :
        string.Equals(role, AGUIRoles.Developer, StringComparison.OrdinalIgnoreCase) ? s_developerChatRole :
        string.Equals(role, AGUIRoles.Tool, StringComparison.OrdinalIgnoreCase) ? ChatRole.Tool :
        throw new InvalidOperationException($"Unknown chat role: {role}");
}
