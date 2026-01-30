// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// JSON converter for polymorphic <see cref="AGUIInputContent"/> deserialization.
/// </summary>
public sealed class AGUIInputContentJsonConverter : JsonConverter<AGUIInputContent>
{
    private const string TypeDiscriminatorPropertyName = "type";

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeof(AGUIInputContent).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override AGUIInputContent Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var jsonElementTypeInfo = options.GetTypeInfo(typeof(JsonElement));
        JsonElement jsonElement = (JsonElement)JsonSerializer.Deserialize(ref reader, jsonElementTypeInfo)!;

        if (!jsonElement.TryGetProperty(TypeDiscriminatorPropertyName, out JsonElement discriminatorElement))
        {
            throw new JsonException($"Missing required property '{TypeDiscriminatorPropertyName}' for AGUIInputContent deserialization");
        }

        string? discriminator = discriminatorElement.GetString();

        AGUIInputContent? result = discriminator switch
        {
            AGUIInputContentTypes.Text => jsonElement.Deserialize(
                options.GetTypeInfo(typeof(AGUITextInputContent))) as AGUITextInputContent,
            AGUIInputContentTypes.Binary => jsonElement.Deserialize(
                options.GetTypeInfo(typeof(AGUIBinaryInputContent))) as AGUIBinaryInputContent,
            _ => throw new JsonException($"Unknown AGUIInputContent type discriminator: '{discriminator}'")
        };

        return result ?? throw new JsonException($"Failed to deserialize AGUIInputContent with type: '{discriminator}'");
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        AGUIInputContent value,
        JsonSerializerOptions options)
    {
        switch (value)
        {
            case AGUITextInputContent text:
                JsonSerializer.Serialize(writer, text, options.GetTypeInfo(typeof(AGUITextInputContent)));
                break;
            case AGUIBinaryInputContent binary:
                JsonSerializer.Serialize(writer, binary, options.GetTypeInfo(typeof(AGUIBinaryInputContent)));
                break;
            default:
                throw new JsonException($"Unknown AGUIInputContent type: {value.GetType().Name}");
        }
    }
}
