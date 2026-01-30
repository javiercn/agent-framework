# AG-UI Multimodal Messages Implementation Plan

## Overview

This document outlines the implementation plan for adding multimodal message support to the AG-UI protocol implementation in the Microsoft Agent Framework .NET SDK, based on the [AG-UI Multimodal Messages Draft Proposal](https://docs.ag-ui.com/drafts/multimodal-messages).

**Status**: Draft Proposal (Implemented in AG-UI spec: October 16, 2025)

### Related Documentation

Before implementing, review the following guidance documents:

- **[src/AGUI.Protocol/AGENTS.md](../src/AGUI.Protocol/AGENTS.md)** - Protocol types, JSON serialization patterns, and instructions for adding new message types
- **[src/Microsoft.Agents.AI.AGUI/AGENTS.md](../src/Microsoft.Agents.AI.AGUI/AGENTS.md)** - Bidirectional mapping strategy between AG-UI and M.E.AI types
- **[AG-UI LLM-Optimized Full Documentation](https://docs.ag-ui.com/llms-full.txt)** - Complete AG-UI protocol specification

### Architecture Context

The multimodal implementation spans two packages:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Client Application                           │
│    Uses ChatMessage with TextContent, DataContent, UriContent   │
├─────────────────────────────────────────────────────────────────┤
│              Microsoft.Agents.AI.AGUI                           │
│    AGUIChatMessageExtensions.AsChatMessages() ← inbound         │
│    AGUIChatMessageExtensions.AsAGUIMessages() → outbound        │
├─────────────────────────────────────────────────────────────────┤
│                    AGUI.Protocol                                │
│    AGUIUserMessage.Content: IList<AGUIInputContent>             │
│    AGUIInputContent → AGUITextInputContent | AGUIBinaryInputContent │
├─────────────────────────────────────────────────────────────────┤
│                    HTTP/SSE Wire Format                         │
│    { "content": "string" } OR { "content": [...InputContent] }  │
│    (string content auto-wrapped to single TextInputContent)     │
└─────────────────────────────────────────────────────────────────┘
```

---

## 1. Summary

### Problem Statement

The current AG-UI .NET implementation only supports text-based user messages via the `AGUIUserMessage.Content` property (string). As LLMs increasingly support multimodal inputs (images, audio, files), the protocol implementation needs to evolve to handle these richer input types.

### Solution

Change `AGUIUserMessage.Content` from `string` to `IList<AGUIInputContent>`. The JSON converter handles the wire format union type (`string | InputContent[]`) transparently:
- **Deserialization**: String content is wrapped in a single `AGUITextInputContent`
- **Serialization**: Single text content is emitted as a string for wire compatibility

### Goal

Extend the AG-UI .NET implementation to support **multimodal input messages**. Since this is pre-release, we can make breaking changes to simplify the API. Inputs may include:
- Text content
- Images (JPEG, PNG, GIF, WebP)
- Audio (WAV, MP3, FLAC)
- Files/Documents (PDF, Office formats, etc.)

### Mapping Strategy

| AG-UI Content Type | M.E.AI Content Type | Notes |
|--------------------|---------------------|-------|
| `TextInputContent` | `TextContent` | Direct text mapping |
| `BinaryInputContent` (with `data`) | `DataContent` | Base64-decoded to bytes |
| `BinaryInputContent` (with `url`) | `UriContent` | URL reference to hosted content |
| `BinaryInputContent` (with `id`) | Custom handling | Reference to pre-uploaded content |

### Key Design Principles (from AGENTS.md)

Following the established patterns documented in the AGENTS.md files:

1. **AG-UI types are the "wire format", MEAI types are the "API surface"**
2. **Preserve `RawRepresentation`** on all mapped types for debugging/logging
3. **Use `AdditionalProperties`** for metadata that doesn't have direct MEAI equivalents
4. **Follow Start-Content-End pattern** for streaming (not applicable for input messages)
5. **Single `Content` property as array** - always use `IList<AGUIInputContent>`, handle string deserialization by wrapping in `AGUITextInputContent`
6. **Wire format compatibility** - serialize single text content as string for interop with other AG-UI SDKs

---

## 2. Detailed Specification

### 2.1 New Types in AGUI.Protocol

Following the patterns from [AGUI.Protocol/AGENTS.md](../src/AGUI.Protocol/AGENTS.md):

- **One class per file** in `Messages/` directory
- **Sealed classes** for concrete types
- **XML Documentation** on all public types and members (`///` comments)
- **Copyright header** on every file: `// Copyright (c) Microsoft. All rights reserved.`
- **JsonPropertyName attributes** with camelCase names
- **JsonIgnore** for conditional serialization of optional properties

#### AGUIInputContent Base Class

```csharp
// File: src/AGUI.Protocol/Messages/AGUIInputContent.cs

namespace AGUI.Protocol;

/// <summary>
/// Base class for multimodal input content in AG-UI messages.
/// </summary>
[JsonConverter(typeof(AGUIInputContentJsonConverter))]
public abstract class AGUIInputContent
{
    /// <summary>
    /// Gets the content type discriminator ("text" or "binary").
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}
```

#### AGUITextInputContent

```csharp
// File: src/AGUI.Protocol/Messages/AGUITextInputContent.cs

namespace AGUI.Protocol;

/// <summary>
/// Represents text content within a multimodal message.
/// </summary>
public sealed class AGUITextInputContent : AGUIInputContent
{
    /// <inheritdoc />
    public override string Type => AGUIInputContentTypes.Text;

    /// <summary>
    /// Gets or sets the text content.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
```

#### AGUIBinaryInputContent

```csharp
// File: src/AGUI.Protocol/Messages/AGUIBinaryInputContent.cs

namespace AGUI.Protocol;

/// <summary>
/// Represents binary content such as images, audio, or files in a multimodal message.
/// </summary>
/// <remarks>
/// <para>Binary content can be provided through multiple methods:</para>
/// <list type="bullet">
///   <item><description><b>Inline Data</b>: Base64-encoded content in the <see cref="Data"/> property.</description></item>
///   <item><description><b>URL Reference</b>: External URL in the <see cref="Url"/> property.</description></item>
///   <item><description><b>ID Reference</b>: Reference to pre-uploaded content via the <see cref="Id"/> property.</description></item>
/// </list>
/// <para>At least one of <see cref="Data"/>, <see cref="Url"/>, or <see cref="Id"/> must be provided.</para>
/// </remarks>
public sealed class AGUIBinaryInputContent : AGUIInputContent
{
    /// <inheritdoc />
    public override string Type => AGUIInputContentTypes.Binary;

    /// <summary>
    /// Gets or sets the MIME type of the content (e.g., "image/jpeg", "audio/wav", "application/pdf").
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional identifier for content reference (for pre-uploaded content).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets an optional URL to fetch the content from.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the optional base64-encoded content data.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Data { get; set; }

    /// <summary>
    /// Gets or sets an optional filename for the content.
    /// </summary>
    [JsonPropertyName("filename")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Filename { get; set; }
}
```

#### AGUIInputContentTypes Constants

```csharp
// File: src/AGUI.Protocol/Messages/AGUIInputContentTypes.cs

namespace AGUI.Protocol;

/// <summary>
/// Contains constants for AG-UI input content type discriminators.
/// </summary>
public static class AGUIInputContentTypes
{
    /// <summary>
    /// Text content type.
    /// </summary>
    public const string Text = "text";

    /// <summary>
    /// Binary content type (images, audio, files).
    /// </summary>
    public const string Binary = "binary";
}
```

### 2.2 Updated AGUIUserMessage

The `AGUIUserMessage` class will have its `Content` property changed from `string` to `IList<AGUIInputContent>`. The JSON converter handles the union type (`string | InputContent[]`) transparently:

```csharp
// File: src/AGUI.Protocol/Messages/AGUIUserMessage.cs

namespace AGUI.Protocol;

/// <summary>
/// Represents a user message in the AG-UI protocol.
/// </summary>
/// <remarks>
/// <para>Content is always represented as an array of <see cref="AGUIInputContent"/> items.</para>
/// <para>For simple text messages, use a single <see cref="AGUITextInputContent"/> element.</para>
/// <para>The JSON converter automatically handles the wire format union type (<c>string | InputContent[]</c>):</para>
/// <list type="bullet">
///   <item><description><b>Deserialization</b>: String content is wrapped in <see cref="AGUITextInputContent"/>.</description></item>
///   <item><description><b>Serialization</b>: Single text content is emitted as a string for wire compatibility.</description></item>
/// </list>
/// </remarks>
public sealed class AGUIUserMessage : AGUIMessage
{
    /// <inheritdoc />
    public override string Role => AGUIRoles.User;

    /// <summary>
    /// Gets or sets the name of the user.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the message content as an array of input content items.
    /// </summary>
    /// <remarks>
    /// This property hides the base <see cref="AGUIMessage.Content"/> string property.
    /// The JSON converter handles the wire format union type (<c>string | InputContent[]</c>).
    /// </remarks>
    [JsonIgnore] // Handled by custom converter
    public new IList<AGUIInputContent> Content { get; set; } = [];
}
```

**Note**: The `new` keyword hides the base class `string Content` property. The JSON converter handles serialization/deserialization of the `content` field appropriately for the wire format.

### 2.3 Custom JSON Handling for User Message Content

Since AG-UI uses `content: string | InputContent[]`, we need special handling for this union type. Following the existing pattern in `AGUIMessageJsonConverter` and `BaseEventJsonConverter`, we use `JsonElement` to inspect the content type before deserializing.

**Note**: The existing converters in this project use `JsonElement` for polymorphic deserialization. This is the established pattern because:
1. It's AOT-safe with source-generated serializers
2. It handles discriminators regardless of property order
3. It allows delegation to the serialization context

The alternative "reader clone" approach (documented in Microsoft docs) has limitations: it can cause stack overflow if the converter is registered in options, and requires the discriminator to appear first.

**Implementation Strategy**: Update `AGUIMessageJsonConverter.Write/Read` to handle `AGUIUserMessage` with the unified `Content` array:

```csharp
// In AGUIMessageJsonConverter.Read - special handling for user messages
case AGUIRoles.User:
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
```

```csharp
// In AGUIMessageJsonConverter.Write - special handling for user messages
case AGUIUserMessage user:
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
    if (user.Content is [AGUITextInputContent { Text: var text }])
    {
        // Single text content -> emit as string for interop with other AG-UI SDKs
        writer.WriteString("content", text);
    }
    else if (user.Content.Count > 0)
    {
        // Multimodal or multiple contents -> emit as array
        writer.WritePropertyName("content");
        writer.WriteStartArray();
        foreach (var content in user.Content)
        {
            JsonSerializer.Serialize(writer, content, options);
        }
        writer.WriteEndArray();
    }
    else
    {
        writer.WriteString("content", string.Empty);
    }
    
    writer.WriteEndObject();
    break;
}
```

### 2.4 AGUIInputContent JSON Converter

```csharp
// File: src/AGUI.Protocol/Messages/AGUIInputContentJsonConverter.cs

namespace AGUI.Protocol;

/// <summary>
/// JSON converter for polymorphic <see cref="AGUIInputContent"/> deserialization.
/// </summary>
public sealed class AGUIInputContentJsonConverter : JsonConverter<AGUIInputContent>
{
    private const string TypeDiscriminatorPropertyName = "type";

    public override bool CanConvert(Type typeToConvert) =>
        typeof(AGUIInputContent).IsAssignableFrom(typeToConvert);

    public override AGUIInputContent Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var jsonElement = JsonElement.ParseValue(ref reader);

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
```

---

## 3. Updates to Microsoft.Agents.AI.AGUI

Following the mapping patterns from [Microsoft.Agents.AI.AGUI/AGENTS.md](../src/Microsoft.Agents.AI.AGUI/AGENTS.md):

### Mapping Direction

The mapping extension methods handle bidirectional conversion:

| Direction | Method | File |
|-----------|--------|------|
| AG-UI → MEAI (Inbound) | `AsChatMessages()` | `AGUIChatMessageExtensions.cs` |
| MEAI → AG-UI (Outbound) | `AsAGUIMessages()` | `AGUIChatMessageExtensions.cs` |

### New Type Mappings to Add

Per the AGENTS.md mapping tables, these new mappings are needed:

| AG-UI Type | MEAI Type | Direction | Notes |
|------------|-----------|-----------|-------|
| `AGUIUserMessage.Contents[]` | `ChatMessage.Contents` | Bidirectional | Multimodal array |
| `AGUITextInputContent` | `TextContent` | Bidirectional | Direct text mapping |
| `AGUIBinaryInputContent` (data) | `DataContent` | Bidirectional | Base64 ↔ bytes |
| `AGUIBinaryInputContent` (url) | `UriContent` | Bidirectional | URL reference |
| `AGUIBinaryInputContent` (id) | `DataContent` + metadata | Inbound only | Requires resolution |

### 3.1 AGUIChatMessageExtensions Updates

Update the `AsChatMessages` method to handle the unified `Content` array:

```csharp
// In AGUIChatMessageExtensions.cs

case AGUIUserMessage userMessage:
{
    var contents = new List<AIContent>();
    
    // Content is always an array (string content was wrapped during deserialization)
    foreach (var inputContent in userMessage.Content)
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
    
    yield return new ChatMessage(role, contents) { MessageId = message.Id };
    break;
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
        dataContent.AdditionalProperties ??= new AdditionalPropertiesDictionary();
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
        var uriContent = new UriContent(binaryContent.Url, binaryContent.MimeType);
        
        uriContent.AdditionalProperties ??= new AdditionalPropertiesDictionary();
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
        
        placeholder.AdditionalProperties ??= new AdditionalPropertiesDictionary();
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
```

### 3.2 Reverse Mapping: ChatMessage to AGUIUserMessage

Update `AsAGUIMessages` to handle multimodal content when converting from M.E.AI back to AG-UI:

```csharp
// In AGUIChatMessageExtensions.cs - AsAGUIMessages method

case { } when message.Role.Value == AGUIRoles.User:
{
    var userMessage = new AGUIUserMessage { Id = message.MessageId };
    
    // Always populate Content array - the JSON converter will optimize wire format
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
                    MimeType = uriContent.MediaType,
                    Url = uriContent.Uri.ToString(),
                    Filename = uriContent.AdditionalProperties?
                        .GetValueOrDefault("filename") as string,
                    Id = uriContent.AdditionalProperties?
                        .GetValueOrDefault("ag_ui_content_id") as string
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
    yield return userMessage;
    break;
}

private static AGUIBinaryInputContent MapDataContentToBinaryInput(DataContent dataContent)
{
    var binary = new AGUIBinaryInputContent
    {
        MimeType = dataContent.MediaType ?? "application/octet-stream",
        Filename = dataContent.AdditionalProperties?.GetValueOrDefault("filename") as string,
        Id = dataContent.AdditionalProperties?.GetValueOrDefault("ag_ui_content_id") as string
    };

    // Check if we should use URL or inline data
    if (dataContent.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
    {
        // Data URI - extract base64 data
        binary.Data = ExtractBase64FromDataUri(dataContent.Uri);
    }
    else if (dataContent.Data.Length > 0)
    {
        // Has byte data - convert to base64
        binary.Data = Convert.ToBase64String(dataContent.Data.ToArray());
    }
    
    return binary;
}
```

---

## 4. Updates to AGUIJsonSerializerContext

Per the AGUI.Protocol/AGENTS.md guidance on AOT compatibility, register the new types for source-generated serialization:

```csharp
// In AGUIJsonSerializerContext.cs - add these attributes

// Input content types for multimodal messages
[JsonSerializable(typeof(AGUIInputContent))]
[JsonSerializable(typeof(AGUIInputContent[]))]
[JsonSerializable(typeof(List<AGUIInputContent>))]
[JsonSerializable(typeof(IList<AGUIInputContent>))]
[JsonSerializable(typeof(AGUITextInputContent))]
[JsonSerializable(typeof(AGUIBinaryInputContent))]
```

### Serialization Guidelines (from AGENTS.md)

- Use `AGUIJsonSerializerContext.Default.Options` for AG-UI types
- Use `options.GetTypeInfo(typeof(T))` for AOT-safe serialization in converters
- Handle both JSON and string content for backward compatibility
- Test with `PublishAot=true` to catch AOT issues early

---

## 5. Implementation Tasks

### Pre-Implementation Checklist (from AGENTS.md)

Before starting implementation:

1. ✅ Review [AGUI.Protocol/AGENTS.md](../src/AGUI.Protocol/AGENTS.md) for protocol patterns
2. ✅ Review [Microsoft.Agents.AI.AGUI/AGENTS.md](../src/Microsoft.Agents.AI.AGUI/AGENTS.md) for mapping patterns
3. ⬜ Fetch latest AG-UI spec from https://docs.ag-ui.com/llms-full.txt
4. ⬜ Review existing `AGUIMessageJsonConverter` implementation for patterns

### Phase 1: Protocol Types (AGUI.Protocol)

| Task | Description | Priority | Estimate |
|------|-------------|----------|----------|
| 1.1 | Create `AGUIInputContent` base class | High | 0.5h |
| 1.2 | Create `AGUITextInputContent` class | High | 0.5h |
| 1.3 | Create `AGUIBinaryInputContent` class | High | 1h |
| 1.4 | Create `AGUIInputContentTypes` constants | High | 0.25h |
| 1.5 | Create `AGUIInputContentJsonConverter` | High | 2h |
| 1.6 | Update `AGUIUserMessage` with `Contents` property | High | 0.5h |
| 1.7 | Update `AGUIMessageJsonConverter` to handle multimodal content | High | 2h |
| 1.8 | Register types in `AGUIJsonSerializerContext` | High | 0.5h |

**Note**: Task 1.7 updates the existing `AGUIMessageJsonConverter` rather than creating a separate converter. This follows the established pattern where all message deserialization is handled in one converter.

### Phase 2: Client Mapping (Microsoft.Agents.AI.AGUI)

| Task | Description | Priority | Estimate |
|------|-------------|----------|----------|
| 2.1 | Update `AsChatMessages` for multimodal user messages | High | 2h |
| 2.2 | Add `MapBinaryContentToAIContent` helper | High | 1.5h |
| 2.3 | Update `AsAGUIMessages` for multimodal output | Medium | 2h |
| 2.4 | Add `MapDataContentToBinaryInput` helper | Medium | 1h |

### Phase 3: Server-Side Support (Microsoft.Agents.AI.Hosting.AGUI.AspNetCore)

| Task | Description | Priority | Estimate |
|------|-------------|----------|----------|
| 3.1 | Verify multimodal messages pass through correctly | Medium | 1h |
| 3.2 | Add integration test with multimodal input | Medium | 2h |

### Phase 4: Testing

Following the test patterns from AGENTS.md:

| Task | Description | Priority | Estimate |
|------|-------------|----------|----------|
| 4.1 | Unit tests for `AGUITextInputContent` serialization | High | 1h |
| 4.2 | Unit tests for `AGUIBinaryInputContent` serialization | High | 2h |
| 4.3 | Unit tests for union type `content` field | High | 2h |
| 4.4 | Round-trip tests (AG-UI → M.E.AI → AG-UI) | High | 2h |
| 4.5 | Backward compatibility tests (string content) | High | 1h |
| 4.6 | Integration tests with image/audio content | Medium | 3h |

#### Test Structure (from AGUI.Protocol/AGENTS.md)

```csharp
[Fact]
public void AGUIBinaryInputContent_SerializesCorrectly()
{
    // Arrange
    var content = new AGUIBinaryInputContent
    {
        MimeType = "image/jpeg",
        Data = "base64data...",
        Filename = "photo.jpg"
    };

    // Act
    var json = JsonSerializer.Serialize(content, AGUIJsonSerializerContext.Default.Options);
    var deserialized = JsonSerializer.Deserialize<AGUIInputContent>(json, AGUIJsonSerializerContext.Default.Options);

    // Assert
    deserialized.Should().BeOfType<AGUIBinaryInputContent>();
    var result = (AGUIBinaryInputContent)deserialized!;
    result.Type.Should().Be(AGUIInputContentTypes.Binary);
    result.MimeType.Should().Be("image/jpeg");
    result.Data.Should().Be("base64data...");
    result.Filename.Should().Be("photo.jpg");
}
```

### Phase 5: Documentation & Samples

| Task | Description | Priority | Estimate |
|------|-------------|----------|----------|
| 5.1 | Update AGENTS.md files with multimodal info | Medium | 1h |
| 5.2 | Add multimodal sample (image analysis) | Medium | 2h |
| 5.3 | Update README with multimodal examples | Low | 1h |

#### AGENTS.md Updates Required

**AGUI.Protocol/AGENTS.md** - Add to "User Message Content Types" section:
- Document `AGUIInputContent` base class
- Document `AGUITextInputContent` and `AGUIBinaryInputContent`
- Update `AGUIUserMessage` documentation to show `Content` as `IList<AGUIInputContent>`

**Microsoft.Agents.AI.AGUI/AGENTS.md** - Add to mapping tables:
- Add multimodal content mappings to "Message Mappings" section
- Document `MapBinaryContentToAIContent` helper
- Document `MapDataContentToBinaryInput` helper

---

## 6. JSON Wire Format Examples

### Simple Text Message (Wire Format)

Single text content is serialized as a string for interoperability with other AG-UI SDKs:

```json
{
  "id": "msg-001",
  "role": "user",
  "content": "What's in this image?"
}
```

This is automatically converted to `[{ "type": "text", "text": "What's in this image?" }]` during deserialization.

### Image with Text (Multimodal)

```json
{
  "id": "msg-002",
  "role": "user",
  "content": [
    {
      "type": "text",
      "text": "What's in this image?"
    },
    {
      "type": "binary",
      "mimeType": "image/jpeg",
      "data": "base64-encoded-image-data..."
    }
  ]
}
```

### Multiple Images with URL References

```json
{
  "id": "msg-003",
  "role": "user",
  "content": [
    {
      "type": "text",
      "text": "Compare these two images"
    },
    {
      "type": "binary",
      "mimeType": "image/png",
      "url": "https://example.com/image1.png"
    },
    {
      "type": "binary",
      "mimeType": "image/png",
      "url": "https://example.com/image2.png"
    }
  ]
}
```

### Audio with Pre-uploaded Reference

```json
{
  "id": "msg-004",
  "role": "user",
  "content": [
    {
      "type": "text",
      "text": "Please transcribe this audio recording"
    },
    {
      "type": "binary",
      "mimeType": "audio/wav",
      "id": "audio-upload-123",
      "filename": "meeting-recording.wav"
    }
  ]
}
```

### Document Analysis

```json
{
  "id": "msg-005",
  "role": "user",
  "content": [
    {
      "type": "text",
      "text": "Summarize the key points from this PDF"
    },
    {
      "type": "binary",
      "mimeType": "application/pdf",
      "filename": "quarterly-report.pdf",
      "url": "https://example.com/reports/q4-2024.pdf"
    }
  ]
}
```

---

## 7. Use Cases

### Visual Question Answering
Users can upload images and ask questions about them. The agent receives the image via `DataContent` or `UriContent` and can pass it to multimodal LLMs like GPT-4V, Claude 3, or Gemini.

### Document Processing
Upload PDFs, Word documents, or spreadsheets for analysis. The agent extracts content and processes it based on user queries.

### Audio Transcription and Analysis
Process voice recordings, podcasts, or meeting audio. Agents can transcribe and analyze audio content.

### Multi-document Comparison
Compare multiple images, documents, or mixed media in a single request.

### Screenshot Analysis
Share screenshots for UI/UX feedback, debugging assistance, or visual documentation.

---

## 8. Testing Strategy

### Unit Tests

1. **Serialization tests** for each content type
2. **Deserialization tests** for union type handling (string → array, array → array)
3. **Round-trip tests** (serialize → deserialize → compare)
4. **String content deserialization** - verify string content is wrapped in `AGUITextInputContent`
5. **Single text serialization** - verify single `AGUITextInputContent` serializes as string
6. **Edge cases**: empty content arrays, null values, invalid MIME types

### Integration Tests

1. **Client-side**: Send multimodal message through `AGUIChatClient`
2. **Server-side**: Receive and process multimodal messages
3. **End-to-end**: Full round-trip through AG-UI stack
4. **Interop tests**: Verify compatibility with TypeScript/Python AG-UI SDKs

### Performance Tests

1. **Large binary payloads**: Test with multi-MB images
2. **Multiple attachments**: Test with 5-10 binary contents
3. **Streaming**: Verify no blocking with large payloads

---

## 9. Dependencies

### External Dependencies
- No new NuGet packages required
- Uses existing `Microsoft.Extensions.AI` types (`DataContent`, `UriContent`, `TextContent`)

### Internal Dependencies
- `AGUI.Protocol` → No dependencies (standalone)
- `Microsoft.Agents.AI.AGUI` → `AGUI.Protocol`, `Microsoft.Extensions.AI`
- `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` → `AGUI.Protocol`

---

## 10. Migration Guide

### Breaking Change Notice

**This is a breaking change** to the `AGUIUserMessage.Content` property type. Since this is pre-release, we can simplify the API without backward compatibility concerns.

### For Users of the Client Library

If you were directly creating `AGUIUserMessage` objects (rare), update to use the new array-based content:

```csharp
// Old (no longer compiles):
var msg = new AGUIUserMessage { Content = "Hello" };

// New:
var msg = new AGUIUserMessage { Content = [new AGUITextInputContent { Text = "Hello" }] };
```

Most users work with M.E.AI `ChatMessage` objects, which are unaffected:

```csharp
// This still works - ChatMessage uses M.E.AI types
var messages = new[] 
{
    new ChatMessage(ChatRole.User, "What's the weather?")
};
```

To use multimodal content, simply include `DataContent` or `UriContent` in the message:

```csharp
// New: multimodal message with image
var imageBytes = await File.ReadAllBytesAsync("photo.jpg");
var messages = new[]
{
    new ChatMessage(ChatRole.User, [
        new TextContent("What's in this image?"),
        new DataContent(imageBytes, "image/jpeg")
    ])
};

// The AG-UI client automatically converts to multimodal format
await foreach (var update in aguiClient.GetStreamingResponseAsync(messages))
{
    Console.Write(update.Text);
}
```

### For Server Implementations

Agents automatically receive multimodal content via the standard `ChatMessage.Contents` collection:

```csharp
public class ImageAnalysisAgent : AIAgent
{
    protected override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        var lastMessage = messages.Last();
        
        foreach (var content in lastMessage.Contents)
        {
            if (content is DataContent imageData && 
                imageData.MediaType?.StartsWith("image/") == true)
            {
                // Process image with vision model
                var analysis = await AnalyzeImageAsync(imageData, cancellationToken);
                yield return new AgentRunResponseUpdate { Text = analysis };
            }
        }
    }
}
```

---

## 11. References

- [AG-UI Multimodal Messages Proposal](https://docs.ag-ui.com/drafts/multimodal-messages)
- [AG-UI LLM-Optimized Full Documentation](https://docs.ag-ui.com/llms-full.txt) - Fetch for complete protocol spec
- [AG-UI GitHub Repository](https://github.com/ag-ui-protocol/ag-ui)
- [OpenAI Vision API](https://platform.openai.com/docs/guides/vision)
- [Anthropic Vision](https://docs.anthropic.com/en/docs/vision)
- [Microsoft.Extensions.AI DataContent](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.datacontent)
- [Microsoft.Extensions.AI UriContent](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.uricontent)
- [Microsoft.Extensions.AI TextContent](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.textcontent)
- [MIME Types (MDN)](https://developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types)

### Internal References

- [AGUI.Protocol/AGENTS.md](../src/AGUI.Protocol/AGENTS.md) - Protocol implementation guidance
- [Microsoft.Agents.AI.AGUI/AGENTS.md](../src/Microsoft.Agents.AI.AGUI/AGENTS.md) - Mapping implementation guidance
- [AGUI-IMPROVEMENTS-PLAN.md](./AGUI-IMPROVEMENTS-PLAN.md) - Overall AG-UI improvements roadmap

---

## 12. Appendix: File Structure

Following the project structure from AGENTS.md files:

```
src/AGUI.Protocol/
├── Messages/
│   ├── AGUIInputContent.cs              [NEW] - Base class with JsonConverter attribute
│   ├── AGUIInputContentJsonConverter.cs [NEW] - Polymorphic deserialization for InputContent
│   ├── AGUIInputContentTypes.cs         [NEW] - "text" and "binary" constants
│   ├── AGUITextInputContent.cs          [NEW] - Text content type
│   ├── AGUIBinaryInputContent.cs        [NEW] - Binary content type
│   ├── AGUIMessage.cs                   [EXISTING]
│   ├── AGUIMessageJsonConverter.cs      [UPDATED] - Handle union type (string | InputContent[])
│   ├── AGUIUserMessage.cs               [UPDATED] - Change Content to IList<AGUIInputContent>
│   ├── AGUIRoles.cs                     [EXISTING]
│   └── ...
├── Serialization/
│   └── AGUIJsonSerializerContext.cs     [UPDATED] - Register new types
└── AGENTS.md                            [UPDATED] - Document new types

src/Microsoft.Agents.AI.AGUI/
├── Shared/
│   └── AGUIChatMessageExtensions.cs     [UPDATED] - Add multimodal mappings
└── AGENTS.md                            [UPDATED] - Document new mappings

tests/AGUI.Protocol.UnitTests/
├── MessageSerializationTests.cs         [UPDATED] - Add multimodal tests
├── MultimodalContentTests.cs            [NEW] - Dedicated multimodal tests
└── ...

tests/Microsoft.Agents.AI.AGUI.UnitTests/
├── MultimodalMappingTests.cs            [NEW] - AG-UI ↔ MEAI mapping tests
└── ...

samples/GettingStarted/AGUI/
└── StepXX_MultimodalMessages/           [NEW]
    ├── Server/
    │   ├── Server.csproj
    │   ├── Program.cs
    │   └── ImageAnalysisAgent.cs
    └── Client/
        ├── Client.csproj
        └── Program.cs
```

---

## 13. Appendix: Implementation Patterns Reference

### Adding New Message Types (from AGUI.Protocol/AGENTS.md)

When adding the new input content types, follow this checklist:

1. ✅ Create new file in `Messages/` using `AGUI` prefix
2. ✅ Inherit from appropriate base class (`AGUIInputContent`)
3. ✅ Override discriminator property (`Type`)
4. ✅ Add constants to `AGUIInputContentTypes.cs`
5. ✅ Register in `AGUIJsonSerializerContext.cs`
6. ✅ Add deserialization support in custom converter
7. ✅ Write unit tests in `tests/AGUI.Protocol.UnitTests/`

### Adding New Mappings (from Microsoft.Agents.AI.AGUI/AGENTS.md)

When adding the multimodal mappings, follow this pattern:

```csharp
// For AG-UI → MEAI (inbound):
case AGUIBinaryInputContent binaryContent:
    yield return new ChatResponseUpdate(ChatRole.User, [MapBinaryContentToAIContent(binaryContent)])
    {
        MessageId = message.Id,
        RawRepresentation = binaryContent  // Always preserve the original
    };
    break;

// For MEAI → AG-UI (outbound):
if (content is DataContent dataContent)
{
    yield return new AGUIBinaryInputContent
    {
        MimeType = dataContent.MediaType ?? "application/octet-stream",
        Data = Convert.ToBase64String(dataContent.Data.ToArray()),
        // Preserve metadata from AdditionalProperties
        Filename = dataContent.AdditionalProperties?.GetValueOrDefault("filename") as string
    };
}
```

### JSON Serialization Pattern (from AGUI.Protocol/AGENTS.md)

```csharp
// Always use camelCase JsonPropertyName
[JsonPropertyName("mimeType")]
public string MimeType { get; set; } = string.Empty;

// Use JsonIgnore for optional properties
[JsonPropertyName("filename")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? Filename { get; set; }

// AOT-safe deserialization in converters
var result = jsonElement.Deserialize(
    options.GetTypeInfo(typeof(AGUIBinaryInputContent))) as AGUIBinaryInputContent;
```
