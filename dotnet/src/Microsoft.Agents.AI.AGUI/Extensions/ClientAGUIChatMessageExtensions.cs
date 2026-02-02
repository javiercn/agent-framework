// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.AGUI.Extensions;

internal static class ClientAGUIChatMessageExtensions
{
    private static readonly ChatRole s_developerChatRole = new("developer");

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
