// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.AGUI;

internal static class AGUIChatMessageExtensions
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
            // The content needs to be resolved by the agent/model
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

    public static IEnumerable<AGUIMessage> AsAGUIMessages(
        this IEnumerable<ChatMessage> chatMessages,
        JsonSerializerOptions jsonSerializerOptions)
    {
        foreach (var message in chatMessages)
        {
            message.MessageId ??= Guid.NewGuid().ToString("N");
            if (message.Role == ChatRole.Tool)
            {
                foreach (var toolMessage in MapToolMessages(jsonSerializerOptions, message))
                {
                    yield return toolMessage;
                }
            }
            else if (message.Role == ChatRole.Assistant)
            {
                var assistantMessage = MapAssistantMessage(jsonSerializerOptions, message);
                if (assistantMessage != null)
                {
                    yield return assistantMessage;
                }
            }
            else if (message.Role == ChatRole.User)
            {
                yield return MapUserMessage(message);
            }
            else
            {
                yield return message.Role.Value switch
                {
                    AGUIRoles.Developer => new AGUIDeveloperMessage { Id = message.MessageId, Content = message.Text ?? string.Empty },
                    AGUIRoles.System => new AGUISystemMessage { Id = message.MessageId, Content = message.Text ?? string.Empty },
                    _ => throw new InvalidOperationException($"Unknown role: {message.Role.Value}")
                };
            }
        }
    }

    private static AGUIUserMessage MapUserMessage(ChatMessage message)
    {
        var userMessage = new AGUIUserMessage { Id = message.MessageId };
        var contents = new List<AGUIInputContent>();

        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case TextContent textContent:
                    contents.Add(new AGUITextInputContent
                    {
                        Text = textContent.Text ?? string.Empty
                    });
                    break;

                case DataContent dataContent:
                    contents.Add(MapDataContentToBinaryInput(dataContent));
                    break;

                case UriContent uriContent:
                    contents.Add(new AGUIBinaryInputContent
                    {
                        MimeType = uriContent.MediaType ?? "application/octet-stream",
                        Url = uriContent.Uri.ToString(),
                        Filename = uriContent.AdditionalProperties?.TryGetValue("filename", out var fn) == true ? fn as string : null,
                        Id = uriContent.AdditionalProperties?.TryGetValue("ag_ui_content_id", out var id) == true ? id as string : null
                    });
                    break;
            }
        }

        // If no contents from the Contents collection, fall back to Text
        if (contents.Count == 0 && !string.IsNullOrEmpty(message.Text))
        {
            contents.Add(new AGUITextInputContent { Text = message.Text });
        }

        userMessage.Content = contents;
        return userMessage;
    }

    private static AGUIBinaryInputContent MapDataContentToBinaryInput(DataContent dataContent)
    {
        var binary = new AGUIBinaryInputContent
        {
            MimeType = dataContent.MediaType ?? "application/octet-stream",
            Filename = dataContent.AdditionalProperties?.TryGetValue("filename", out var fn) == true ? fn as string : null,
            Id = dataContent.AdditionalProperties?.TryGetValue("ag_ui_content_id", out var id) == true ? id as string : null
        };

        // Check if we have inline data
        if (!dataContent.Data.IsEmpty)
        {
            // Has byte data - convert to base64
#if NETSTANDARD2_0 || NET472
            binary.Data = Convert.ToBase64String(dataContent.Data.ToArray());
#else
            binary.Data = Convert.ToBase64String(dataContent.Data.Span);
#endif
        }

        return binary;
    }

    private static AGUIAssistantMessage? MapAssistantMessage(JsonSerializerOptions jsonSerializerOptions, ChatMessage message)
    {
        List<AGUIToolCall>? toolCalls = null;
        string? textContent = null;

        foreach (var content in message.Contents)
        {
            if (content is FunctionCallContent functionCall)
            {
                var argumentsJson = functionCall.Arguments is null ?
                    "{}" :
                    JsonSerializer.Serialize(functionCall.Arguments, jsonSerializerOptions.GetTypeInfo(typeof(IDictionary<string, object?>)));
                toolCalls ??= [];
                toolCalls.Add(new AGUIToolCall
                {
                    Id = functionCall.CallId,
                    Type = "function",
                    Function = new AGUIFunctionCall
                    {
                        Name = functionCall.Name,
                        Arguments = argumentsJson
                    }
                });
            }
            else if (content is TextContent textContentItem)
            {
                textContent = textContentItem.Text;
            }
        }

        // Create message with tool calls and/or text content
        if (toolCalls?.Count > 0 || !string.IsNullOrEmpty(textContent))
        {
            return new AGUIAssistantMessage
            {
                Id = message.MessageId,
                Content = textContent ?? string.Empty,
                ToolCalls = toolCalls?.Count > 0 ? toolCalls.ToArray() : null
            };
        }

        return null;
    }

    private static IEnumerable<AGUIToolMessage> MapToolMessages(JsonSerializerOptions jsonSerializerOptions, ChatMessage message)
    {
        foreach (var content in message.Contents)
        {
            if (content is FunctionResultContent functionResult)
            {
                yield return new AGUIToolMessage
                {
                    Id = functionResult.CallId,
                    ToolCallId = functionResult.CallId,
                    Content = functionResult.Result is null ?
                        string.Empty :
                        JsonSerializer.Serialize(functionResult.Result, jsonSerializerOptions.GetTypeInfo(functionResult.Result.GetType()))
                };
            }
        }
    }

    public static ChatRole MapChatRole(string role) =>
        string.Equals(role, AGUIRoles.System, StringComparison.OrdinalIgnoreCase) ? ChatRole.System :
        string.Equals(role, AGUIRoles.User, StringComparison.OrdinalIgnoreCase) ? ChatRole.User :
        string.Equals(role, AGUIRoles.Assistant, StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant :
        string.Equals(role, AGUIRoles.Developer, StringComparison.OrdinalIgnoreCase) ? s_developerChatRole :
        string.Equals(role, AGUIRoles.Tool, StringComparison.OrdinalIgnoreCase) ? ChatRole.Tool :
        throw new InvalidOperationException($"Unknown chat role: {role}");
}
