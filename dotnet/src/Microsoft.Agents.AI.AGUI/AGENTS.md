# AGENTS.md for Microsoft.Agents.AI.AGUI

This file provides context and guidance for AI coding agents working on the Microsoft.Agents.AI.AGUI library.

## Project Overview

Microsoft.Agents.AI.AGUI provides an **IChatClient** implementation that bridges the **AG-UI Protocol** to **Microsoft.Extensions.AI (MEAI)** abstractions. This enables .NET applications to communicate with AG-UI compliant servers using the familiar MEAI interfaces.

### Purpose

The library serves as a **translation layer** between two different AI communication paradigms:
- **AG-UI Protocol**: An open, event-based streaming protocol using Server-Sent Events (SSE)
- **Microsoft.Extensions.AI**: Microsoft's standard abstractions for AI services (`IChatClient`, `ChatMessage`, `ChatResponseUpdate`)

### Package Information

- **Package Name**: `Microsoft.Agents.AI.AGUI`
- **Namespace**: `Microsoft.Agents.AI.AGUI`
- **Target Frameworks**: net8.0, net9.0, net10.0, netstandard2.0, net472
- **Primary Dependencies**:
  - `AGUI.Protocol` - AG-UI event types and serialization
  - `Microsoft.Extensions.AI` - MEAI abstractions and `FunctionInvokingChatClient`
  - `System.Net.ServerSentEvents` - SSE stream parsing

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    AGUIChatClient                               │
│  (DelegatingChatClient wrapping FunctionInvokingChatClient)     │
├─────────────────────────────────────────────────────────────────┤
│                FunctionInvokingChatClient                       │
│    (Handles client-side tool invocation automatically)          │
├─────────────────────────────────────────────────────────────────┤
│                  AGUIChatClientHandler                          │
│  (Inner IChatClient that does AG-UI → MEAI translation)         │
├─────────────────────────────────────────────────────────────────┤
│                     AGUIHttpService                             │
│    (HTTP POST + SSE stream parsing using AGUI.Protocol)         │
└─────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
src/Microsoft.Agents.AI.AGUI/
├── AGUIChatClient.cs               # Main IChatClient implementation
├── AGUIHttpService.cs              # HTTP/SSE transport layer
├── Shared/
│   ├── AGUIChatMessageExtensions.cs      # ChatMessage ↔ AGUIMessage mappings
│   ├── AIToolExtensions.cs                # AITool ↔ AGUITool mappings
│   └── ChatResponseUpdateAGUIExtensions.cs # Events ↔ ChatResponseUpdate mappings
└── Microsoft.Agents.AI.AGUI.csproj
```

## Mapping Strategy

The library uses **bidirectional mapping** between AG-UI and MEAI types. The key principle is:

> **AG-UI types are the "wire format" (SSE), MEAI types are the "API surface"**

### Direction of Data Flow

```
Client App → MEAI Types → [Mapping] → AG-UI Types → HTTP/SSE → AG-UI Server
Client App ← MEAI Types ← [Mapping] ← AG-UI Types ← HTTP/SSE ← AG-UI Server
```

## Type Mappings Reference

### Role Mappings (AGUIChatMessageExtensions.cs)

AG-UI roles map to MEAI `ChatRole`:

| AG-UI Role (`AGUIRoles`) | MEAI Role (`ChatRole`) |
|--------------------------|------------------------|
| `"system"` | `ChatRole.System` |
| `"user"` | `ChatRole.User` |
| `"assistant"` | `ChatRole.Assistant` |
| `"tool"` | `ChatRole.Tool` |
| `"developer"` | `new ChatRole("developer")` |

**Note**: The `developer` role is not a built-in `ChatRole` constant, so we create a custom one.

### Message Mappings (AGUIChatMessageExtensions.cs)

#### MEAI → AG-UI (Outbound: `AsAGUIMessages`)

| MEAI Message Type | AG-UI Message Type | Mapping Details |
|-------------------|-------------------|-----------------|
| `ChatMessage` (Role=System) | `AGUISystemMessage` | `Content = message.Text` |
| `ChatMessage` (Role=User) | `AGUIUserMessage` | `Content = message.Text` |
| `ChatMessage` (Role=Assistant) | `AGUIAssistantMessage` | Text + optional `ToolCalls` |
| `ChatMessage` (Role=Tool) with `FunctionResultContent` | `AGUIToolMessage` | `ToolCallId`, `Content` (JSON-serialized result) |
| `ChatMessage` (Role=Developer) | `AGUIDeveloperMessage` | `Content = message.Text` |

**Key Mapping Logic for Assistant Messages**:
```csharp
// FunctionCallContent → AGUIToolCall
new AGUIToolCall {
    Id = functionCall.CallId,
    Type = "function",
    Function = new AGUIFunctionCall {
        Name = functionCall.Name,
        Arguments = JsonSerializer.Serialize(functionCall.Arguments)  // Dict → JSON string
    }
}
```

#### AG-UI → MEAI (Inbound: `AsChatMessages`)

| AG-UI Message Type | MEAI Message Type | Mapping Details |
|-------------------|-------------------|-----------------|
| `AGUISystemMessage` | `ChatMessage(ChatRole.System, content)` | Direct text mapping |
| `AGUIUserMessage` | `ChatMessage(ChatRole.User, content)` | Direct text mapping |
| `AGUIAssistantMessage` (no tools) | `ChatMessage(ChatRole.Assistant, content)` | Direct text mapping |
| `AGUIAssistantMessage` (with tools) | `ChatMessage` with `FunctionCallContent` items | Tool calls parsed |
| `AGUIToolMessage` | `ChatMessage(ChatRole.Tool, [FunctionResultContent])` | Result deserialized |
| `AGUIDeveloperMessage` | `ChatMessage(developer role, content)` | Custom role |

**Key Mapping Logic for Tool Messages**:
```csharp
// AGUIToolMessage → FunctionResultContent
new FunctionResultContent(
    toolMessage.ToolCallId,
    JsonSerializer.Deserialize<JsonElement>(toolMessage.Content)  // Try JSON, fallback to string
)
```

### Tool Mappings (AIToolExtensions.cs)

#### MEAI → AG-UI (`AsAGUITools`)

| MEAI Type | AG-UI Type | Notes |
|-----------|-----------|-------|
| `AIFunctionDeclaration` | `AGUITool` | Metadata only (Name, Description, JsonSchema) |
| `AIFunction` | `AGUITool` | Same as declaration - implementation stays client-side |

```csharp
new AGUITool {
    Name = function.Name,
    Description = function.Description,
    Parameters = function.JsonSchema  // JsonElement containing JSON Schema
}
```

**Important**: Only tool **declarations** are sent to the server. The **executable implementation** remains on the client and is invoked by `FunctionInvokingChatClient`.

#### AG-UI → MEAI (`AsAITools`)

```csharp
AIFunctionFactory.CreateDeclaration(
    name: tool.Name,
    description: tool.Description,
    jsonSchema: tool.Parameters
)
```

**Note**: These are **declaration-only** - cannot be invoked, as the implementation exists on the AG-UI server.

### Event → ChatResponseUpdate Mappings (ChatResponseUpdateAGUIExtensions.cs)

#### AG-UI Events → MEAI Updates (`AsChatResponseUpdatesAsync`)

| AG-UI Event | MEAI Content/Update | Notes |
|-------------|---------------------|-------|
| `RunStartedEvent` | Empty update, sets `ConversationId`/`ResponseId` | Lifecycle start |
| `RunFinishedEvent` | Update with result text | Lifecycle end |
| `RunErrorEvent` | `ErrorContent` | Error handling |
| `TextMessageStartEvent` | (Builder state) | Tracks role/messageId |
| `TextMessageContentEvent` | `ChatResponseUpdate(role, delta)` | Streaming text |
| `TextMessageEndEvent` | (Builder cleanup) | Message complete |
| `ToolCallStartEvent` | (Accumulator state) | Tracks toolCallId/name |
| `ToolCallArgsEvent` | (Accumulates JSON) | Args streamed as fragments |
| `ToolCallEndEvent` | `ChatResponseUpdate` with `FunctionCallContent` | Complete tool call |
| `ToolCallResultEvent` | `ChatResponseUpdate` with `FunctionResultContent` | Server tool result |
| `StateSnapshotEvent` | `DataContent("application/json")` | State as JSON bytes |
| `StateDeltaEvent` | `DataContent("application/json-patch+json")` | JSON Patch |
| `ReasoningMessageContentEvent` | `TextReasoningContent` | Chain-of-thought |
| `CustomEvent` | Update with `RawRepresentation` | Pass-through |

#### MEAI Updates → AG-UI Events (`AsAGUIEventStreamAsync`)

| MEAI Content Type | AG-UI Event(s) | Notes |
|-------------------|----------------|-------|
| `TextContent` | `TextMessageStart`, `TextMessageContent`, `TextMessageEnd` | Full streaming pattern |
| `FunctionCallContent` | `ToolCallStart`, `ToolCallArgs`, `ToolCallEnd` | Full streaming pattern |
| `FunctionResultContent` | `ToolCallResultEvent` | Single event |
| `TextReasoningContent` | `ReasoningStart`, `ReasoningMessageStart/Content/End`, `ReasoningEnd` | Full reasoning pattern |
| `DataContent("application/json")` | `StateSnapshotEvent` | State snapshot |
| `DataContent("application/json-patch+json")` | `StateDeltaEvent` | JSON Patch delta |
| (raw `BaseEvent`) | Pass-through | Direct emit |

## Adding New Mappings

When AG-UI protocol adds new event types or when MEAI adds new content types, follow this strategy:

### Step 1: Determine Direction

- **AG-UI → MEAI**: Event arrives from server, needs to become `ChatResponseUpdate`
- **MEAI → AG-UI**: Content from client needs to become AG-UI event stream

### Step 2: Choose the Appropriate Extension

| Mapping Type | File | Method |
|--------------|------|--------|
| Message ↔ Message | `AGUIChatMessageExtensions.cs` | `AsChatMessages`/`AsAGUIMessages` |
| Tool ↔ Tool | `AIToolExtensions.cs` | `AsAITools`/`AsAGUITools` |
| Event → Update | `ChatResponseUpdateAGUIExtensions.cs` | `AsChatResponseUpdatesAsync` |
| Update → Event | `ChatResponseUpdateAGUIExtensions.cs` | `AsAGUIEventStreamAsync` |

### Step 3: Follow the Pattern

#### For Event → ChatResponseUpdate:

```csharp
case NewEventType newEvent:
    yield return new ChatResponseUpdate(ChatRole.Assistant, [new SomeContent(...)])
    {
        ConversationId = conversationId,
        ResponseId = responseId,
        MessageId = newEvent.MessageId,
        CreatedAt = DateTimeOffset.UtcNow,
        RawRepresentation = newEvent  // Always preserve the original event
    };
    break;
```

#### For ChatResponseUpdate → Event:

```csharp
if (content is NewContentType newContent)
{
    yield return new NewAGUIEvent
    {
        // Map properties from newContent to AG-UI event
    };
}
```

### Step 4: Handle Streaming Patterns

AG-UI uses Start/Content/End patterns for streaming. Use builders/accumulators:

```csharp
// For multi-part events (text messages, tool calls, reasoning)
private sealed class SomeBuilder
{
    private string? _currentId;
    
    public void Start(StartEvent evt) { _currentId = evt.Id; }
    public ChatResponseUpdate EmitContent(ContentEvent evt) { /* emit update */ }
    public void End(EndEvent evt) { _currentId = null; }
}
```

### Step 5: Update Tests

Add tests in `tests/Microsoft.Agents.AI.AGUI.UnitTests/`:
- Round-trip serialization tests
- Edge case handling (null values, empty strings)
- Error scenarios

## Thread ID / Conversation ID Handling

AG-UI requires a `threadId` on every request. MEAI uses `ConversationId` to track conversations.

**Key insight**: `FunctionInvokingChatClient` interprets `ConversationId` as meaning "the underlying client manages history" and won't send full message history on subsequent turns. AG-UI **always** requires full history.

**Solution**: 
1. Extract `ConversationId` from options and store in `AdditionalProperties["agui_thread_id"]`
2. Clear `ConversationId` before passing to `FunctionInvokingChatClient`
3. Restore it on output updates

```csharp
// On input
innerOptions.AdditionalProperties["agui_thread_id"] = options.ConversationId;
innerOptions.ConversationId = null;  // Prevent FICC from skipping history

// On output
finalUpdate.ConversationId = threadId;  // Restore for caller
```

## Interrupt / Resume Mappings (Human-in-the-Loop)

AG-UI supports **interrupts** for human-in-the-loop scenarios. The library provides bidirectional mapping between AG-UI interrupt types and MEAI's experimental interrupt content types via `InterruptContentExtensions`.

### MEAI Interrupt Types (Experimental - MEAI001)

| MEAI Type | Purpose | Key Properties |
|-----------|---------|----------------|
| `FunctionApprovalRequestContent` | Agent requests approval before executing a function | `Id`, `FunctionCall` |
| `FunctionApprovalResponseContent` | User's approval/rejection decision | `Id`, `Approved`, `FunctionCall` |
| `UserInputRequestContent` | Agent requests additional user input | `Id` |
| `UserInputResponseContent` | User's response to input request | `Id` |

**Note**: The MEAI interrupt types are experimental (`#pragma warning disable MEAI001`). The `UserInputRequestContent` and `UserInputResponseContent` have protected constructors, so the library provides derived types: `AGUIUserInputRequestContent` and `AGUIUserInputResponseContent`.

### AG-UI Interrupt Types

| AG-UI Type | Purpose | Key Properties |
|------------|---------|----------------|
| `AGUIInterrupt` | Interrupt payload in `RunFinishedEvent` | `Id`, `Payload` (JsonElement) |
| `AGUIResume` | Resume payload in `RunAgentInput` | `InterruptId`, `Payload` (JsonElement) |
| `RunFinishedOutcome` | Constants: `Success`, `Interrupt` | - |

### Mapping Direction: AG-UI → MEAI (Inbound)

#### `InterruptContentExtensions.FromAGUIInterrupt(AGUIInterrupt)`

Converts an AG-UI interrupt to MEAI content based on payload structure:

| AG-UI Payload Pattern | MEAI Content Type | Detection Logic |
|----------------------|-------------------|-----------------|
| `{ "functionName": "...", "functionArguments": {...} }` | `FunctionApprovalRequestContent` | Has `functionName` property |
| `{ "prompt": "...", ... }` or any other | `AGUIUserInputRequestContent` | Fallback for non-function payloads |
| No payload | `AGUIUserInputRequestContent` | Empty payload defaults to user input |

#### `InterruptContentExtensions.FromAGUIResume(AGUIResume, AIContent?)`

Converts an AG-UI resume to MEAI content based on original interrupt:

| Original Interrupt Type | Resume Payload | MEAI Content Type |
|------------------------|----------------|-------------------|
| `FunctionApprovalRequestContent` | `{ "approved": true/false }` | `FunctionApprovalResponseContent` |
| `UserInputRequestContent` | Any JSON | `AGUIUserInputResponseContent` |
| Unknown | Any JSON | `AGUIUserInputResponseContent` |

### Mapping Direction: MEAI → AG-UI (Outbound)

#### `InterruptContentExtensions.ToAGUIInterrupt(FunctionApprovalRequestContent)`

```csharp
// Creates AGUIInterrupt with function details in payload
new AGUIInterrupt
{
    Id = approvalRequest.Id,
    Payload = JsonDocument.Parse("""
        {
            "functionName": "...",
            "functionArguments": {...}
        }
        """).RootElement
}
```

#### `InterruptContentExtensions.ToAGUIInterrupt(UserInputRequestContent)`

```csharp
// If RawRepresentation is AGUIInterrupt, returns it directly
// If RawRepresentation is JsonElement, uses as payload
// Otherwise creates minimal interrupt
new AGUIInterrupt
{
    Id = inputRequest.Id,
    Payload = inputRequest.RawRepresentation as JsonElement?
}
```

#### `InterruptContentExtensions.ToAGUIResume(FunctionApprovalResponseContent)`

```csharp
new AGUIResume
{
    InterruptId = approvalResponse.Id,
    Payload = JsonDocument.Parse("""{"approved": true}""").RootElement
}
```

#### `InterruptContentExtensions.ToAGUIResume(UserInputResponseContent)`

```csharp
new AGUIResume
{
    InterruptId = inputResponse.Id,
    Payload = inputResponse.RawRepresentation as JsonElement?
}
```

### Integration with Event Stream

The `ChatResponseUpdateAGUIExtensions.AsAGUIEventStreamAsync` method handles interrupts:

1. When encountering `FunctionApprovalRequestContent` or `UserInputRequestContent`:
   - Converts to `AGUIInterrupt` using `ToAGUIInterrupt`
   - Marks the run builder for interrupt outcome

2. `ValidateAndEmitRunFinished` emits:
   ```csharp
   new RunFinishedEvent
   {
       Outcome = RunFinishedOutcome.Interrupt,  // "interrupt"
       Interrupt = interrupt
   }
   ```

### Integration with Hosting Layer

The `AGUIEndpointRouteBuilderExtensions.MapPostRunAgent` method handles resume:

1. Deserializes `RunAgentInput` from request body
2. If `input.Resume` is present:
   - Converts to MEAI content using `FromAGUIResume`
   - Adds to the last message's content for the agent to process

### Usage Example: Function Approval Flow

```csharp
// 1. Agent requests approval (server-side)
var approvalRequest = new FunctionApprovalRequestContent(
    "call_abc",
    new FunctionCallContent("call_abc", "delete_file", args));

// 2. Converts to AG-UI interrupt for wire transmission
var interrupt = InterruptContentExtensions.ToAGUIInterrupt(approvalRequest, options);
// Sent as: {"id":"call_abc","payload":{"functionName":"delete_file","functionArguments":{...}}}

// 3. Client receives interrupt, user approves
var resume = new AGUIResume { InterruptId = "call_abc", Payload = JsonDocument.Parse("""{"approved":true}""").RootElement };

// 4. Client sends resume, server converts back to MEAI
var content = InterruptContentExtensions.FromAGUIResume(resume, originalRequest);
// Returns FunctionApprovalResponseContent with Approved = true
```

## State Management

AG-UI state (`StateSnapshotEvent`, `StateDeltaEvent`) maps to MEAI `DataContent`:

- **State Snapshot**: `DataContent` with `"application/json"` media type
- **State Delta**: `DataContent` with `"application/json-patch+json"` media type

State can be sent TO the server by including `DataContent` in the last message's contents.

## Build Commands

```bash
# Build the library
dotnet build src/Microsoft.Agents.AI.AGUI

# Run unit tests
dotnet test tests/Microsoft.Agents.AI.AGUI.UnitTests

# Run with verbose output
dotnet test tests/Microsoft.Agents.AI.AGUI.UnitTests -v n
```

## Code Style Guidelines

### General Conventions

- Follow Microsoft.Extensions.AI patterns and naming
- Use `internal static class` for extension methods (shared via `InternalsVisibleTo`)
- Preserve `RawRepresentation` on all mapped types for debugging/logging

### JSON Serialization

- Use `AGUIJsonSerializerContext.Default.Options` for AG-UI types
- Use `jsonSerializerOptions.GetTypeInfo(typeof(T))` for AOT-safe serialization
- Handle both JSON and string content for tool results

### Error Handling

- Validate event sequences (e.g., `ToolCallEnd` must have matching `ToolCallStart`)
- Throw `InvalidOperationException` for protocol violations
- Use `ErrorContent` for AG-UI `RunErrorEvent`

## Related Projects

- **AGUI.Protocol**: AG-UI event types and serialization context
- **Microsoft.Agents.AI.Hosting.AGUI.AspNetCore**: Server-side AG-UI hosting
- **Microsoft.Agents.AI.Abstractions**: Shared abstractions like `AgentResponseUpdate`

## MEAI Reference

For detailed MEAI documentation, consult:
- [IChatClient Interface](https://learn.microsoft.com/en-us/dotnet/ai/ichatclient)
- [Microsoft.Extensions.AI Overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [AIContent Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.aicontent)
- [ChatOptions Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatoptions)

Key MEAI types used:
- `IChatClient` / `DelegatingChatClient` - Chat abstraction
- `ChatMessage` / `ChatRole` - Message representation
- `ChatResponseUpdate` - Streaming response
- `FunctionCallContent` / `FunctionResultContent` - Tool calling
- `TextContent` / `DataContent` - Content types
- `AITool` / `AIFunctionDeclaration` / `AIFunction` - Tool definitions
- `FunctionInvokingChatClient` - Automatic tool invocation

### AIContent Type Hierarchy

`AIContent` is the base class for all content types in MEAI. It uses `System.Text.Json` polymorphic serialization with a `$type` discriminator.

#### Core AIContent Types

| Type | Description | Usage |
|------|-------------|-------|
| `TextContent` | Represents text content in chat messages | Primary content type for user/assistant messages |
| `DataContent` | Binary content with a media type (MIME) | Images, audio, state snapshots, custom binary data |
| `UriContent` | URL reference to hosted content | Links to images, audio, video at remote URLs |
| `FunctionCallContent` | Request to invoke a function/tool | Emitted by assistant when calling tools |
| `FunctionResultContent` | Result from function/tool invocation | Response to a function call, contains result data |
| `ErrorContent` | Represents a non-fatal error | Used for errors where operation continues |
| `UsageContent` | Token usage information | Input/output token counts for billing/limits |
| `TextReasoningContent` | Chain-of-thought reasoning text | Model's internal reasoning process (thinking) |

#### Extended AIContent Types (Specialized Use Cases)

| Type | Description | When to Use |
|------|-------------|-------------|
| `HostedFileContent` | Reference to a server-hosted file | File uploads, code interpreter outputs |
| `HostedVectorStoreContent` | Reference to a vector store | RAG scenarios with hosted embeddings |
| `CodeInterpreterToolCallContent` | Code interpreter tool invocation | When model wants to execute code |
| `CodeInterpreterToolResultContent` | Code interpreter results | Outputs from code execution |
| `ImageGenerationToolCallContent` | Image generation request | When model wants to generate images |
| `ImageGenerationToolResultContent` | Generated image results | Outputs from image generation |
| `McpServerToolCallContent` | MCP server tool invocation | Model Context Protocol tool calls |
| `McpServerToolResultContent` | MCP tool results | Results from MCP tool execution |
| `FunctionApprovalRequestContent` | Human approval request | Human-in-the-loop approval workflows |
| `FunctionApprovalResponseContent` | Human approval response | User's approval/rejection decision |
| `UserInputRequestContent` | Request for user input | Agent needs additional user information |
| `UserInputResponseContent` | User input response | User's response to input request |

#### AIContent Common Properties

All `AIContent` types inherit these properties:

```csharp
public class AIContent
{
    // Additional metadata for provider-specific data
    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
    
    // Annotations (e.g., citations, metadata)
    public IList<AIContentAnnotation>? Annotations { get; set; }
    
    // Original provider representation for debugging
    public object? RawRepresentation { get; set; }
}
```

#### AIContent Usage Examples

**TextContent** - Simple text:
```csharp
var content = new TextContent("Hello, world!");
```

**DataContent** - Binary data with media type:
```csharp
// From bytes
var imageContent = new DataContent(imageBytes, "image/png");

// State snapshot (JSON)
var stateContent = new DataContent(
    Encoding.UTF8.GetBytes(jsonState), 
    "application/json"
);

// JSON Patch delta
var deltaContent = new DataContent(
    Encoding.UTF8.GetBytes(jsonPatch), 
    "application/json-patch+json"
);
```

**FunctionCallContent** - Tool invocation:
```csharp
var toolCall = new FunctionCallContent(
    callId: "call_abc123",
    name: "get_weather",
    arguments: new Dictionary<string, object?> 
    { 
        ["location"] = "Seattle",
        ["units"] = "fahrenheit"
    }
);
```

**FunctionResultContent** - Tool result:
```csharp
var result = new FunctionResultContent(
    callId: "call_abc123",
    result: new { temperature = 72, conditions = "sunny" }
);

// With exception
var errorResult = new FunctionResultContent(
    callId: "call_abc123",
    exception: new InvalidOperationException("API unavailable")
);
```

**ErrorContent** - Non-fatal error:
```csharp
var error = new ErrorContent(
    message: "Rate limit exceeded, retrying...",
    errorCode: "rate_limit"
);
```

**TextReasoningContent** - Model reasoning/thinking:
```csharp
var reasoning = new TextReasoningContent("Let me think about this step by step...");
```

**UsageContent** - Token usage tracking:
```csharp
var usage = new UsageContent(new UsageDetails
{
    InputTokenCount = 150,
    OutputTokenCount = 250,
    TotalTokenCount = 400
});
```

### ChatOptions Properties

`ChatOptions` controls the behavior of chat completion requests:

#### Model Selection

| Property | Type | Description |
|----------|------|-------------|
| `ModelId` | `string?` | The model ID to use (e.g., "gpt-4o", "claude-3-sonnet") |

#### Generation Parameters

| Property | Type | Description | Typical Range |
|----------|------|-------------|---------------|
| `Temperature` | `float?` | Controls randomness. Lower = more deterministic | 0.0 - 2.0 |
| `TopP` | `float?` | Nucleus sampling - considers tokens with top_p probability mass | 0.0 - 1.0 |
| `TopK` | `int?` | Limits to top K most probable tokens | 1 - vocab size |
| `MaxOutputTokens` | `int?` | Maximum tokens in generated response | > 0 |
| `FrequencyPenalty` | `float?` | Penalizes repeated tokens proportionally | -2.0 - 2.0 |
| `PresencePenalty` | `float?` | Penalizes tokens that have appeared at all | -2.0 - 2.0 |
| `Seed` | `long?` | Seed for reproducible generation | Any long |
| `StopSequences` | `IList<string>?` | Sequences that stop generation when encountered | - |

#### Conversation Context

| Property | Type | Description |
|----------|------|-------------|
| `ConversationId` | `string?` | Associates request with existing conversation |
| `Instructions` | `string?` | Per-request instructions (added to system context) |
| `ContinuationToken` | `string?` | Token for resuming/getting background response |
| `AllowBackgroundResponses` | `bool?` | Whether background/async responses are allowed |

#### Tool/Function Calling

| Property | Type | Description |
|----------|------|-------------|
| `Tools` | `IList<AITool>?` | Available tools for the model to invoke |
| `ToolMode` | `ChatToolMode?` | Controls tool calling behavior |
| `AllowMultipleToolCalls` | `bool?` | Whether single response can include multiple tool calls |

**ChatToolMode Options**:
- `ChatToolMode.Auto` - Model decides when to call tools
- `ChatToolMode.RequireAny` - Model must call at least one tool
- `ChatToolMode.RequireSpecific(functionName)` - Model must call specific tool
- `ChatToolMode.None` - Disable tool calling

#### Response Format

| Property | Type | Description |
|----------|------|-------------|
| `ResponseFormat` | `ChatResponseFormat?` | Controls output format |

**ChatResponseFormat Options**:
```csharp
// Free-form text (default)
ChatResponseFormat.Text

// JSON output (model decides structure)
ChatResponseFormat.Json

// Structured JSON with schema
ChatResponseFormat.ForJsonSchema(
    schema: AIJsonUtilities.CreateJsonSchema(typeof(MyClass)),
    schemaName: "MyClass",
    schemaDescription: "Description of the structure"
)
```

#### Extensibility

| Property | Type | Description |
|----------|------|-------------|
| `AdditionalProperties` | `AdditionalPropertiesDictionary?` | Provider-specific options |
| `RawRepresentationFactory` | `Func<...>?` | Factory for native request objects |

#### ChatOptions Usage Examples

**Basic options**:
```csharp
var options = new ChatOptions
{
    Temperature = 0.7f,
    MaxOutputTokens = 2000
};
```

**Deterministic output**:
```csharp
var options = new ChatOptions
{
    Temperature = 0.0f,
    Seed = 42
};
```

**Tool calling**:
```csharp
var options = new ChatOptions
{
    Tools = [AIFunctionFactory.Create(GetWeather)],
    ToolMode = ChatToolMode.Auto
};
```

**Structured output**:
```csharp
var options = new ChatOptions
{
    ResponseFormat = ChatResponseFormat.ForJsonSchema(
        AIJsonUtilities.CreateJsonSchema(typeof(PersonInfo)),
        "PersonInfo",
        "Information about a person")
};
```

**With conversation context**:
```csharp
var options = new ChatOptions
{
    ConversationId = threadId,
    Instructions = "Be concise and use bullet points."
};
```

### AG-UI to MEAI Content Type Mapping Summary

| AG-UI Event/Type | MEAI Content Type | Notes |
|------------------|-------------------|-------|
| `TextMessageContentEvent` | `TextContent` | Streaming text delta |
| `ToolCallEndEvent` | `FunctionCallContent` | Complete tool invocation |
| `ToolCallResultEvent` | `FunctionResultContent` | Server-side tool result |
| `RunErrorEvent` | `ErrorContent` | Error during run |
| `StateSnapshotEvent` | `DataContent("application/json")` | State as JSON |
| `StateDeltaEvent` | `DataContent("application/json-patch+json")` | JSON Patch |
| `ReasoningMessageContentEvent` | `TextReasoningContent` | Model thinking |
| - | `UsageContent` | Token usage (from response metadata) |
