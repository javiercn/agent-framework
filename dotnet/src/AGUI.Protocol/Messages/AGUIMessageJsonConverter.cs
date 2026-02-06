// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// JSON converter for polymorphic <see cref="AGUIMessage"/> deserialization.
/// </summary>
public sealed class AGUIMessageJsonConverter : JsonConverter<AGUIMessage>
{
    private const string RoleDiscriminatorPropertyName = "role";

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeof(AGUIMessage).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override AGUIMessage Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var jsonElementTypeInfo = options.GetTypeInfo(typeof(JsonElement));
        JsonElement jsonElement = (JsonElement)JsonSerializer.Deserialize(ref reader, jsonElementTypeInfo)!;

        // Try to get the discriminator property
        if (!jsonElement.TryGetProperty(RoleDiscriminatorPropertyName, out JsonElement discriminatorElement))
        {
            throw new JsonException($"Missing required property '{RoleDiscriminatorPropertyName}' for AGUIMessage deserialization");
        }

        string? discriminator = discriminatorElement.GetString();

        // Map discriminator to concrete type and deserialize using type info from options
        AGUIMessage? result = discriminator switch
        {
            AGUIRoles.Developer => jsonElement.Deserialize(options.GetTypeInfo(typeof(AGUIDeveloperMessage))) as AGUIDeveloperMessage,
            AGUIRoles.System => jsonElement.Deserialize(options.GetTypeInfo(typeof(AGUISystemMessage))) as AGUISystemMessage,
            AGUIRoles.User => DeserializeUserMessage(jsonElement, options),
            AGUIRoles.Assistant => jsonElement.Deserialize(options.GetTypeInfo(typeof(AGUIAssistantMessage))) as AGUIAssistantMessage,
            AGUIRoles.Tool => jsonElement.Deserialize(options.GetTypeInfo(typeof(AGUIToolMessage))) as AGUIToolMessage,
            _ => throw new JsonException($"Unknown AGUIMessage role discriminator: '{discriminator}'")
        };

        if (result == null)
        {
            throw new JsonException($"Failed to deserialize AGUIMessage with role discriminator: '{discriminator}'");
        }

        return result;
    }

    private static AGUIUserMessage DeserializeUserMessage(JsonElement jsonElement, JsonSerializerOptions options)
    {
        var userMessage = new AGUIUserMessage
        {
            Id = jsonElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null,
            Name = jsonElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null,
        };

        if (jsonElement.TryGetProperty("content", out var contentProp))
        {
            if (contentProp.ValueKind == JsonValueKind.String)
            {
                // String content -> wrap in AGUITextInputContent
                userMessage.Content = [new AGUITextInputContent { Text = contentProp.GetString() ?? string.Empty }];
            }
            else if (contentProp.ValueKind == JsonValueKind.Array)
            {
                // Multimodal content array
                var contents = new List<AGUIInputContent>();
                foreach (var element in contentProp.EnumerateArray())
                {
                    if (!element.TryGetProperty("type", out var typeProp))
                    {
                        throw new JsonException("Missing 'type' discriminator in InputContent");
                    }

                    var contentType = typeProp.GetString();
                    AGUIInputContent? inputContent = contentType switch
                    {
                        AGUIInputContentTypes.Text => element.Deserialize(
                            options.GetTypeInfo(typeof(AGUITextInputContent))) as AGUITextInputContent,
                        AGUIInputContentTypes.Binary => element.Deserialize(
                            options.GetTypeInfo(typeof(AGUIBinaryInputContent))) as AGUIBinaryInputContent,
                        _ => throw new JsonException($"Unknown InputContent type: '{contentType}'")
                    };

                    if (inputContent is not null)
                    {
                        contents.Add(inputContent);
                    }
                }
                userMessage.Content = contents;
            }
        }

        return userMessage;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        AGUIMessage value,
        JsonSerializerOptions options)
    {
        // Serialize the concrete type directly using type info from options
        switch (value)
        {
            case AGUIDeveloperMessage developer:
                JsonSerializer.Serialize(writer, developer, options.GetTypeInfo(typeof(AGUIDeveloperMessage)));
                break;
            case AGUISystemMessage system:
                JsonSerializer.Serialize(writer, system, options.GetTypeInfo(typeof(AGUISystemMessage)));
                break;
            case AGUIUserMessage user:
                WriteUserMessage(writer, user, options);
                break;
            case AGUIAssistantMessage assistant:
                JsonSerializer.Serialize(writer, assistant, options.GetTypeInfo(typeof(AGUIAssistantMessage)));
                break;
            case AGUIToolMessage tool:
                JsonSerializer.Serialize(writer, tool, options.GetTypeInfo(typeof(AGUIToolMessage)));
                break;
            default:
                throw new JsonException($"Unknown AGUIMessage type: {value.GetType().Name}");
        }
    }

    private static void WriteUserMessage(Utf8JsonWriter writer, AGUIUserMessage user, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (user.Id is not null)
        {
            writer.WriteString("id", user.Id);
        }

        writer.WriteString("role", user.Role);

        if (user.Name is not null)
        {
            writer.WriteString("name", user.Name);
        }

        // Write content - emit as string if single text content for wire compatibility
        if (user.Content.Count == 1 && user.Content[0] is AGUITextInputContent singleTextContent)
        {
            // Single text content -> emit as string for interop with other AG-UI SDKs
            writer.WriteString("content", singleTextContent.Text);
        }
        else if (user.Content.Count > 0)
        {
            // Multimodal or multiple contents -> emit as array
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            foreach (var content in user.Content)
            {
                JsonSerializer.Serialize(writer, content, options.GetTypeInfo(typeof(AGUIInputContent)));
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteString("content", string.Empty);
        }

        writer.WriteEndObject();
    }
}
