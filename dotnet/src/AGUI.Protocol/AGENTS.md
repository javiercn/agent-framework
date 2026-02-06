# AGENTS.md for AGUI.Protocol

This file provides context and guidance for AI coding agents working on the AGUI.Protocol library.

## Protocol References

**Important**: Before making changes to this library, fetch the complete AG-UI protocol documentation:

- **LLM-Optimized Full Documentation**: https://docs.ag-ui.com/llms-full.txt
  - Fetch this URL to get complete AG-UI protocol documentation in a single text file
  - Contains all event types, message formats, architecture details, and SDK references
- Official Documentation: https://docs.ag-ui.com/
- AG-UI GitHub: https://github.com/ag-ui-protocol/ag-ui

## Project Overview

AGUI.Protocol is a standalone .NET library that implements the **AG-UI (Agent-User Interaction) Protocol** - an open, event-based standard for communication between AI agents and user-facing applications. This library provides all the types needed to implement AG-UI compliant agents and clients using Server-Sent Events (SSE).

### What is AG-UI?

AG-UI is one of three complementary open agentic protocols:
- **AG-UI (Agent–User Interaction)**: Connects agents to users through user-facing applications
- **MCP (Model Context Protocol)**: Connects agents to tools and data sources  
- **A2A (Agent to Agent)**: Connects agents to other agents

AG-UI standardizes how agent state, UI intents, and user interactions flow between model/agent runtimes and frontend applications.

### Core Design Principles

1. **Event-Driven Architecture**: All communication happens through strongly-typed events streamed over SSE
2. **Bi-Directional Communication**: Both agents and frontends can send and receive events
3. **Transport Agnostic**: Protocol works over HTTP/SSE, WebSockets, or other transports
4. **Vendor Neutral**: Messages and events are standardized across different AI providers

### Package Information

- **Package Name**: `AGUI.Protocol`
- **Namespace**: `AGUI.Protocol`
- **Target Frameworks**: net8.0, net9.0, net10.0, netstandard2.0, net472
- **Primary Dependency**: `System.Text.Json`

## Project Structure

```
src/AGUI.Protocol/
├── Context/           # Input types (RunAgentInput, AGUIContextItem)
├── Events/            # All 24+ AG-UI event types and base classes
├── Messages/          # Message types (User, Assistant, Tool, System, Developer)
├── Serialization/     # JSON serialization context for AOT support
├── Tools/             # Tool-related types (AGUITool, AGUIToolCall, AGUIFunctionCall)
├── AGUIPropertyNames.cs  # JSON property name constants
└── README.md          # Public-facing documentation
```

## Build Commands

```bash
# Build the library
dotnet build src/AGUI.Protocol

# Run unit tests
dotnet test tests/AGUI.Protocol.UnitTests

# Build and test all AGUI-related projects
dotnet test --filter "FullyQualifiedName~AGUI"
```

## Code Style Guidelines

### General Conventions

- **One class per file**: Each event, message, or type gets its own file
- **Sealed classes**: Event types should be `sealed` unless inheritance is needed
- **XML Documentation**: All public types and members must have `///` documentation
- **Copyright header**: Every file starts with `// Copyright (c) Microsoft. All rights reserved.`

### Naming Conventions

- **Event types**: End with `Event` suffix (e.g., `RunStartedEvent`, `TextMessageContentEvent`)
- **Message types**: Use `AGUI` prefix (e.g., `AGUIMessage`, `AGUIUserMessage`)
- **Constants classes**: Use plural form (e.g., `AGUIEventTypes`, `AGUIRoles`)
- **Property names**: Use PascalCase for C# properties, camelCase in JSON

### JSON Serialization Patterns

The library uses System.Text.Json source generation for AOT compatibility:

```csharp
// Always use JsonPropertyName attribute with camelCase
[JsonPropertyName("messageId")]
public string MessageId { get; set; } = string.Empty;

// Use JsonIgnore for conditional serialization
[JsonPropertyName("parentMessageId")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? ParentMessageId { get; set; }
```

### Event Type Pattern

All events inherit from `BaseEvent` and must:
1. Override the `Type` property to return the appropriate constant from `AGUIEventTypes`
2. Be registered in `AGUIJsonSerializerContext` for serialization support
3. Have a corresponding deserialization case in `BaseEventJsonConverter`

```csharp
// Example event implementation
public sealed class TextMessageStartEvent : BaseEvent
{
    /// <inheritdoc />
    public override string Type => AGUIEventTypes.TextMessageStart;

    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}
```

### Message Type Pattern

All messages inherit from `AGUIMessage` and must:
1. Override the `Role` property to return the appropriate constant from `AGUIRoles`
2. Be registered in `AGUIJsonSerializerContext`
3. Have a corresponding deserialization case in `AGUIMessageJsonConverter`

## AG-UI Event Categories

Events are the fundamental units of communication in AG-UI. All events share common base properties inherited from `BaseEvent`:

```typescript
interface BaseEvent {
  type: EventType      // The specific event type identifier (discriminator)
  timestamp?: number   // Optional timestamp when the event was created
  rawEvent?: any       // Optional field containing original event data if transformed
}
```

### Lifecycle Events

Monitor the progression of agent runs. A typical run: `RunStarted` → (optional `StepStarted`/`StepFinished` pairs) → `RunFinished` or `RunError`.

#### RunStartedEvent
Signals the start of an agent run.
```typescript
type RunStartedEvent = BaseEvent & {
  type: "RUN_STARTED"
  threadId: string        // ID of the conversation thread
  runId: string           // ID of the agent run
  parentRunId?: string    // Optional lineage pointer for branching/time travel
  input?: RunAgentInput   // Optional exact agent input payload for this run
}
```

#### RunFinishedEvent
Signals the successful completion of an agent run.
```typescript
type RunFinishedEvent = BaseEvent & {
  type: "RUN_FINISHED"
  threadId: string    // ID of the conversation thread
  runId: string       // ID of the agent run
  result?: any        // Optional result data from the agent run
}
```

#### RunErrorEvent
Signals an error during an agent run.
```typescript
type RunErrorEvent = BaseEvent & {
  type: "RUN_ERROR"
  message: string     // Error message
  code?: string       // Optional error code
}
```

#### StepStartedEvent / StepFinishedEvent
Signal the start/completion of a step within an agent run.
```typescript
type StepStartedEvent = BaseEvent & {
  type: "STEP_STARTED"
  stepName: string    // Name of the step
}

type StepFinishedEvent = BaseEvent & {
  type: "STEP_FINISHED"
  stepName: string    // Name of the step
}
```

### Text Message Events (Start → Content* → End pattern)

Handle streaming textual content. Enables real-time display as content is generated.

#### TextMessageStartEvent
Signals the start of a text message.
```typescript
type TextMessageStartEvent = BaseEvent & {
  type: "TEXT_MESSAGE_START"
  messageId: string              // Unique identifier for the message
  role: "assistant"              // Role is always "assistant"
}
```

#### TextMessageContentEvent
Represents a chunk of content in a streaming text message.
```typescript
type TextMessageContentEvent = BaseEvent & {
  type: "TEXT_MESSAGE_CONTENT"
  messageId: string    // Matches the ID from TextMessageStart
  delta: string        // Text content chunk (non-empty)
}
```

#### TextMessageEndEvent
Signals the end of a text message.
```typescript
type TextMessageEndEvent = BaseEvent & {
  type: "TEXT_MESSAGE_END"
  messageId: string    // Matches the ID from TextMessageStart
}
```

#### TextMessageChunkEvent
Convenience event that auto-expands to Start→Content→End.
```typescript
type TextMessageChunkEvent = BaseEvent & {
  type: "TEXT_MESSAGE_CHUNK"
  messageId?: string   // Required on first chunk for a message
  role?: "developer" | "system" | "assistant" | "user"  // Defaults to "assistant"
  delta?: string       // Text content
}
```

### Tool Call Events (Start → Args* → End pattern)

Manage tool executions by agents. Frontends define tools and pass them to agents.

#### ToolCallStartEvent
Signals the start of a tool call.
```typescript
type ToolCallStartEvent = BaseEvent & {
  type: "TOOL_CALL_START"
  toolCallId: string        // Unique identifier for the tool call
  toolCallName: string      // Name of the tool being called
  parentMessageId?: string  // Optional ID of the parent message
}
```

#### ToolCallArgsEvent
Represents a chunk of argument data for a tool call.
```typescript
type ToolCallArgsEvent = BaseEvent & {
  type: "TOOL_CALL_ARGS"
  toolCallId: string    // Matches the ID from ToolCallStart
  delta: string         // Argument data chunk (JSON fragment)
}
```

#### ToolCallEndEvent
Signals the end of a tool call.
```typescript
type ToolCallEndEvent = BaseEvent & {
  type: "TOOL_CALL_END"
  toolCallId: string    // Matches the ID from ToolCallStart
}
```

#### ToolCallResultEvent
Provides the result of a tool call execution.
```typescript
type ToolCallResultEvent = BaseEvent & {
  type: "TOOL_CALL_RESULT"
  messageId: string     // ID of the conversation message this result belongs to
  toolCallId: string    // Matches the ID from ToolCallStart
  content: string       // The actual result/output content from tool execution
  role?: "tool"         // Optional role identifier
}
```

#### ToolCallChunkEvent
Convenience event that auto-expands to Start→Args→End.
```typescript
type ToolCallChunkEvent = BaseEvent & {
  type: "TOOL_CALL_CHUNK"
  toolCallId?: string       // Required on first chunk
  toolCallName?: string     // Required on first chunk
  parentMessageId?: string  // Optional parent message ID
  delta?: string            // Argument data chunk (JSON fragment)
}
```

### State Management Events (Snapshot-Delta pattern)

Synchronize state between agents and UI using JSON Patch (RFC 6902).

#### StateSnapshotEvent
Provides a complete snapshot of an agent's state.
```typescript
type StateSnapshotEvent = BaseEvent & {
  type: "STATE_SNAPSHOT"
  snapshot: any    // Complete state snapshot
}
```

#### StateDeltaEvent
Provides a partial update to an agent's state using JSON Patch.
```typescript
type StateDeltaEvent = BaseEvent & {
  type: "STATE_DELTA"
  delta: JsonPatchOperation[]    // Array of JSON Patch operations (RFC 6902)
}

// JSON Patch operation structure
interface JsonPatchOperation {
  op: "add" | "remove" | "replace" | "move" | "copy" | "test"
  path: string      // JSON Pointer (RFC 6901) to target location
  value?: any       // Value to apply (for add, replace)
  from?: string     // Source path (for move, copy)
}
```

#### MessagesSnapshotEvent
Provides a snapshot of all messages in a conversation.
```typescript
type MessagesSnapshotEvent = BaseEvent & {
  type: "MESSAGES_SNAPSHOT"
  messages: Message[]    // Array of message objects
}
```

### Activity Events

Represent ongoing activity progress for frontend-only UI updates (not sent to agent).

#### ActivitySnapshotEvent
Delivers a complete snapshot of an activity message.
```typescript
type ActivitySnapshotEvent = BaseEvent & {
  type: "ACTIVITY_SNAPSHOT"
  messageId: string             // Identifier for the ActivityMessage
  activityType: string          // Activity discriminator (e.g., "PLAN", "SEARCH")
  content: Record<string, any>  // Structured payload representing activity state
  replace?: boolean             // Defaults to true; false ignores if message exists
}
```

#### ActivityDeltaEvent
Applies incremental updates to an existing activity using JSON Patch.
```typescript
type ActivityDeltaEvent = BaseEvent & {
  type: "ACTIVITY_DELTA"
  messageId: string     // Identifier for the target activity message
  activityType: string  // Activity discriminator
  patch: any[]          // RFC 6902 JSON Patch operations
}
```

### Reasoning Events (Draft/Proposed)

Support LLM reasoning visibility and chain-of-thought reasoning.

#### ReasoningStartEvent / ReasoningEndEvent
```typescript
type ReasoningStartEvent = BaseEvent & {
  type: "REASONING_START"
  messageId: string           // Unique identifier of this reasoning
  encryptedContent?: string   // Optional encrypted content
}

type ReasoningEndEvent = BaseEvent & {
  type: "REASONING_END"
  messageId: string    // Unique identifier of this reasoning
}
```

#### ReasoningMessageStartEvent / ReasoningMessageContentEvent / ReasoningMessageEndEvent
```typescript
type ReasoningMessageStartEvent = BaseEvent & {
  type: "REASONING_MESSAGE_START"
  messageId: string     // Unique identifier of the message
  role: "assistant"     // Role of the reasoning message
}

type ReasoningMessageContentEvent = BaseEvent & {
  type: "REASONING_MESSAGE_CONTENT"
  messageId: string     // Matches ID from ReasoningMessageStart
  delta: string         // Reasoning content chunk (non-empty)
}

type ReasoningMessageEndEvent = BaseEvent & {
  type: "REASONING_MESSAGE_END"
  messageId: string     // Matches ID from ReasoningMessageStart
}
```

#### ReasoningMessageChunkEvent
```typescript
type ReasoningMessageChunkEvent = BaseEvent & {
  type: "REASONING_MESSAGE_CHUNK"
  messageId?: string    // Message ID (first chunk must be non-empty)
  delta?: string        // Reasoning content chunk
}
```

### Interrupt / Resume Pattern (Human-in-the-Loop)

AG-UI supports **interrupts** for human-in-the-loop scenarios where agents pause execution to request user approval or input before proceeding.

#### Interrupt Types

1. **Function Approval**: Agent requests user approval before executing a sensitive function
2. **User Input**: Agent requests additional information from the user

#### RunFinishedEvent with Interrupt

When an agent needs to pause for user interaction, it emits a `RunFinishedEvent` with an `outcome` of `"interrupt"`:

```typescript
type RunFinishedEvent = BaseEvent & {
  type: "RUN_FINISHED"
  threadId: string
  runId: string
  result?: any
  outcome?: "success" | "interrupt"  // Optional outcome indicator
  interrupt?: AGUIInterrupt          // Present when outcome is "interrupt"
}

type AGUIInterrupt = {
  id: string         // Unique identifier for this interrupt (used to match resume)
  payload?: any      // Context-specific data (function details, prompt, etc.)
}
```

**Function Approval Interrupt Payload**:
```json
{
  "id": "call_abc123",
  "payload": {
    "functionName": "delete_file",
    "functionArguments": {
      "path": "/important.txt"
    }
  }
}
```

**User Input Interrupt Payload**:
```json
{
  "id": "input_789",
  "payload": {
    "prompt": "Please enter your API key",
    "inputType": "password"
  }
}
```

#### Resuming from an Interrupt

To resume execution after user response, include an `AGUIResume` in the `RunAgentInput`:

```typescript
interface RunAgentInput {
  threadId: string
  runId: string
  messages: Message[]
  tools: Tool[]
  context: Context[]
  state: any
  forwardedProps?: any
  resume?: AGUIResume  // Present when resuming from an interrupt
}

interface AGUIResume {
  interruptId: string  // Must match the interrupt's id
  payload?: any        // User's response data
}
```

**Function Approval Resume Payload**:
```json
{
  "interruptId": "call_abc123",
  "payload": {
    "approved": true
  }
}
```

**User Input Resume Payload**:
```json
{
  "interruptId": "input_789",
  "payload": {
    "response": "sk-secret-key-123"
  }
}
```

#### Interrupt Flow Diagram

```
┌──────────┐                    ┌──────────┐                    ┌──────────┐
│  Client  │                    │  AG-UI   │                    │  Agent   │
│   App    │                    │  Server  │                    │ Runtime  │
└────┬─────┘                    └────┬─────┘                    └────┬─────┘
     │                               │                               │
     │  POST /run {messages, tools}  │                               │
     │──────────────────────────────>│  Run agent                    │
     │                               │──────────────────────────────>│
     │                               │                               │
     │                               │  FunctionApprovalRequest      │
     │                               │<──────────────────────────────│
     │  SSE: RUN_FINISHED            │                               │
     │  (outcome: "interrupt")       │                               │
     │<──────────────────────────────│                               │
     │                               │                               │
     │  [User approves in UI]        │                               │
     │                               │                               │
     │  POST /run {resume: {...}}    │                               │
     │──────────────────────────────>│  Resume with approval         │
     │                               │──────────────────────────────>│
     │                               │                               │
     │                               │  Continue execution           │
     │                               │<──────────────────────────────│
     │  SSE: RUN_FINISHED            │                               │
     │  (outcome: "success")         │                               │
     │<──────────────────────────────│                               │
     │                               │                               │
```

### Special Events

#### RawEvent
Used to pass through events from external systems.
```typescript
type RawEvent = BaseEvent & {
  type: "RAW"
  event: any         // Original event data
  source?: string    // Optional source identifier
}
```

#### CustomEvent
Used for application-specific custom events.
```typescript
type CustomEvent = BaseEvent & {
  type: "CUSTOM"
  name: string    // Name of the custom event
  value: any      // Value associated with the event
}
```

#### MetaEvent (Draft)
Side-band annotation event that can occur anywhere in the stream.
```typescript
type MetaEvent = BaseEvent & {
  type: "META"
  metaType: string              // Application-defined type (e.g., "thumbs_up", "tag")
  payload: Record<string, any>  // Application-defined payload
}
```

## Adding New Event Types

When adding a new event type:

1. Create a new file in `Events/` following the naming convention
2. Inherit from `BaseEvent` and override `Type`
3. Add the event type constant to `AGUIEventTypes.cs`
4. Register the type in `AGUIJsonSerializerContext.cs`
5. Add deserialization support in `BaseEventJsonConverter.cs`
6. Write unit tests in `tests/AGUI.Protocol.UnitTests/EventSerializationTests.cs`

## Adding New Message Types

When adding a new message type:

1. Create a new file in `Messages/` using `AGUI` prefix
2. Inherit from `AGUIMessage` and override `Role`
3. Add the role constant to `AGUIRoles.cs`
4. Register the type in `AGUIJsonSerializerContext.cs`
5. Add deserialization support in `AGUIMessageJsonConverter.cs`
6. Write unit tests in `tests/AGUI.Protocol.UnitTests/MessageSerializationTests.cs`

## Testing Instructions

### Unit Test Structure

Tests use xUnit and FluentAssertions:

```csharp
[Fact]
public void EventName_SerializesCorrectly()
{
    // Arrange
    var evt = new SomeEvent { /* properties */ };

    // Act
    var json = JsonSerializer.Serialize(evt, s_options);
    var deserialized = JsonSerializer.Deserialize<BaseEvent>(json, s_options);

    // Assert
    deserialized.Should().BeOfType<SomeEvent>();
    var result = (SomeEvent)deserialized!;
    result.Type.Should().Be(AGUIEventTypes.SomeType);
    // Assert other properties...
}
```

### Running Tests

```bash
# Run all AGUI.Protocol unit tests
dotnet test tests/AGUI.Protocol.UnitTests

# Run specific test
dotnet test tests/AGUI.Protocol.UnitTests --filter "FullyQualifiedName~TextMessageStartEvent"

# Run with verbose output
dotnet test tests/AGUI.Protocol.UnitTests -v n
```

## Related Projects

This library is used by:

- **Microsoft.Agents.AI.AGUI**: Chat client implementation for AG-UI servers
- **Microsoft.Agents.AI.Hosting.AGUI.AspNetCore**: ASP.NET Core hosting for AG-UI agents

## Key Protocol Concepts

### Event Flow Patterns

1. **Start-Content-End Pattern**: Used for streaming content (text messages, tool calls)
   - Start event initiates the stream with unique ID
   - Content events deliver data chunks (concatenate `delta` values in order)
   - End event signals completion
   - Frontends should handle events in order and associate by ID

2. **Snapshot-Delta Pattern**: Used for state synchronization
   - Snapshot provides complete state (replace existing state entirely)
   - Delta events provide incremental updates using JSON Patch (RFC 6902)
   - If inconsistencies detected, request fresh snapshot

3. **Lifecycle Pattern**: Used for monitoring agent runs
   - `RunStarted` and either `RunFinished` or `RunError` are mandatory
   - Step events are optional for granular progress tracking
   - `parentRunId` enables branching and time travel (git-like append-only log)

### Message Types and Roles

AG-UI messages are vendor-neutral and can be mapped to/from proprietary AI provider formats. Messages follow a discriminated union pattern based on the `role` field.

```typescript
type Role = "developer" | "system" | "assistant" | "user" | "tool" | "activity"
```

#### BaseMessage (common fields)
```typescript
interface BaseMessage {
  id: string        // Unique identifier for the message
  role: Role        // Discriminator field
  content?: string  // Optional text content
  name?: string     // Optional name of the sender
}
```

#### DeveloperMessage
Internal messages used for development or debugging.
```typescript
interface DeveloperMessage {
  id: string
  role: "developer"
  content: string    // Text content (required)
  name?: string
}
```

#### SystemMessage
System instructions or context provided to the agent.
```typescript
interface SystemMessage {
  id: string
  role: "system"
  content: string    // Instructions or context (required)
  name?: string
}
```

#### AssistantMessage
Messages from the AI assistant to the user.
```typescript
interface AssistantMessage {
  id: string
  role: "assistant"
  content?: string          // Text response (optional if using tool calls)
  name?: string
  toolCalls?: ToolCall[]    // Optional tool calls made by the assistant
}
```

#### UserMessage
Messages from the end user to the agent.
```typescript
interface UserMessage {
  id: string
  role: "user"
  content: string | InputContent[]   // Text or multimodal input
  name?: string
}
```

#### ToolMessage
Results from tool executions.
```typescript
interface ToolMessage {
  id: string
  role: "tool"
  content: string       // Result from tool execution
  toolCallId: string    // ID of the tool call this responds to
  error?: string        // Error message if tool call failed
}
```

#### ActivityMessage
Structured UI messages that exist only on the frontend (not sent to model).
```typescript
interface ActivityMessage {
  id: string
  role: "activity"
  activityType: string           // e.g., "PLAN", "SEARCH", "SCRAPE"
  content: Record<string, any>   // Structured payload rendered by frontend
}
```

### User Message Content Types

User messages support multimodal input via `InputContent[]`:

```typescript
type InputContent = TextInputContent | BinaryInputContent

interface TextInputContent {
  type: "text"
  text: string
}

interface BinaryInputContent {
  type: "binary"
  mimeType: string       // e.g., "image/jpeg", "audio/wav", "application/pdf"
  id?: string            // Reference to pre-uploaded content
  url?: string           // URL to fetch the content
  data?: string          // Base64-encoded content
  filename?: string      // Optional filename
}
// Note: At least one of id, url, or data must be provided for BinaryInputContent
```

### ToolCall Structure

Tool calls embedded within assistant messages:
```typescript
interface ToolCall {
  id: string             // Unique ID for this tool call
  type: "function"       // Type is always "function"
  function: FunctionCall
}

interface FunctionCall {
  name: string           // Name of the function to call
  arguments: string      // JSON-encoded string of arguments
}
```

### Tools (Frontend-Defined)

Tools are defined in the frontend and passed to agents during execution. This enables:
- Frontend control over agent capabilities
- Dynamic capabilities based on user permissions
- Security (sensitive operations controlled by application)

Tool structure follows JSON Schema:
```typescript
interface Tool {
  name: string           // Unique identifier for the tool
  description: string    // Human-readable explanation of what the tool does
  parameters: {          // JSON Schema defining tool parameters
    type: "object"
    properties: {
      // Tool-specific parameters with types and descriptions
    }
    required: string[]   // Array of required parameter names
  }
}
```

Example tool definition:
```typescript
const confirmAction: Tool = {
  name: "confirmAction",
  description: "Ask the user to confirm a specific action before proceeding",
  parameters: {
    type: "object",
    properties: {
      action: {
        type: "string",
        description: "The action that needs user confirmation"
      },
      importance: {
        type: "string",
        enum: ["low", "medium", "high", "critical"],
        description: "The importance level of the action"
      }
    },
    required: ["action"]
  }
}
```

Tool call lifecycle: `ToolCallStart` → `ToolCallArgs*` → `ToolCallEnd` → (execution) → `ToolCallResult`

### Context

Contextual information provided to an agent:
```typescript
interface Context {
  description: string    // Description of what this context represents
  value: string          // The actual context value
}
```

### RunAgentInput

Input parameters for running an agent (HTTP POST body):
```typescript
interface RunAgentInput {
  threadId: string           // ID of the conversation thread
  runId: string              // ID of the current run
  parentRunId?: string       // Optional ID of the run that spawned this run
  state: any                 // Current state of the agent
  messages: Message[]        // Array of messages in the conversation
  tools: Tool[]              // Array of tools available to the agent
  context: Context[]         // Array of context objects provided to the agent
  forwardedProps: any        // Additional properties forwarded to the agent
}
```

### State Management

State is a structured data object that:
- Persists across interactions
- Can be accessed by both agent and frontend
- Updates in real-time via Snapshot/Delta pattern
- Enables human-in-the-loop collaboration

```typescript
type State = any   // Flexible to hold any data structure
```

#### JSON Patch Format (RFC 6902)

AG-UI uses JSON Patch for state deltas:

```typescript
interface JsonPatchOperation {
  op: "add" | "remove" | "replace" | "move" | "copy" | "test"
  path: string      // JSON Pointer (RFC 6901) to target location
  value?: any       // Value to apply (for add, replace)
  from?: string     // Source path (for move, copy)
}
```

Common operations:
```json
// add - Adds a value to an object or array
{ "op": "add", "path": "/user/preferences", "value": { "theme": "dark" } }

// replace - Replaces a value
{ "op": "replace", "path": "/conversation_state", "value": "paused" }

// remove - Removes a value
{ "op": "remove", "path": "/temporary_data" }

// move - Moves a value from one location to another
{ "op": "move", "path": "/completed_items", "from": "/pending_items/0" }

// copy - Copies a value
{ "op": "copy", "path": "/backup", "from": "/current" }

// test - Tests a value for equality (for validation)
{ "op": "test", "path": "/version", "value": 2 }
```

### Serialization and History

Event streams can be serialized for:
- Restore chat history after reloads/reconnects
- Attach to running agents
- Create branches (time travel) from any prior run
- Compact stored history (merge chunks into snapshots)

Compaction rules:
- Message streams: Combine `TEXT_MESSAGE_*` sequences into single snapshot
- Tool calls: Collapse start/args/end into compact record
- State: Merge consecutive deltas into final snapshot

### Serialization

AG-UI serialization has **two distinct dimensions** that work together:

#### 1. Client-Side Event Stream Serialization

Serialize the stream of `BaseEvent` objects for history persistence:

```csharp
// Capture events during streaming
List<BaseEvent> allEvents = [];
await foreach (var update in agent.RunStreamingAsync(messages, session))
{
    if (update.RawRepresentation is BaseEvent evt)
        allEvents.Add(evt);
}

// Serialize events to JSON for storage
string json = JsonSerializer.Serialize(
    allEvents.ToArray(), 
    AGUIJsonSerializerContext.Default.Options);

// Deserialize events from stored JSON
BaseEvent[] restored = JsonSerializer.Deserialize<BaseEvent[]>(
    json, 
    AGUIJsonSerializerContext.Default.Options);
```

#### 2. Server-Side Session Persistence

Use `AgentSessionStore` with `MapAGUI` to persist sessions across requests:

```csharp
// Server setup with session persistence
InMemoryAgentSessionStore sessionStore = new();
app.MapAGUI("/", agent, sessionStore);  // Sessions persisted by threadId
```

The AG-UI `threadId` maps to the conversation identifier in `AgentSessionStore`.

#### Branching with ParentRunId

`RunStartedEvent` supports branching via `parentRunId`:

```csharp
new RunStartedEvent 
{ 
    ThreadId = "thread_abc", 
    RunId = "run_002",
    ParentRunId = "run_001",  // Branch from run_001
    Input = new RunAgentInput { /* optional: exact input for this run */ }
}
```

This creates a git-like append-only log where each run can branch from any previous run.

### Transport

AG-UI uses Server-Sent Events (SSE) over HTTP for streaming. Events are serialized as JSON:

```
data: {"type":"TEXT_MESSAGE_CONTENT","messageId":"msg_123","delta":"Hello"}\n\n
```

### Generative UI Support

AG-UI is a **User Interaction protocol** (not a generative UI spec) that provides bi-directional runtime connection. It natively supports:
- A2UI (Google) - Declarative, JSONL-based streaming UI
- Open-JSON-UI (OpenAI) - OpenAI's declarative UI schema
- MCP-UI (Microsoft + Shopify) - iframe-based UI extending MCP
- Custom generative UI standards

## Common Tasks

### Serialize an Event

```csharp
using System.Text.Json;
using AGUI.Protocol;

var evt = new TextMessageContentEvent
{
    MessageId = "msg_123",
    Delta = "Hello world"
};

var json = JsonSerializer.Serialize(evt, AGUIJsonSerializerContext.Default.Options);
```

### Serialize Event Array for History Persistence

```csharp
using System.Text.Json;
using AGUI.Protocol;

// Collect events during a conversation
var events = new BaseEvent[]
{
    new RunStartedEvent { ThreadId = "t1", RunId = "r1" },
    new TextMessageStartEvent { MessageId = "m1", Role = AGUIRoles.Assistant },
    new TextMessageContentEvent { MessageId = "m1", Delta = "Hello!" },
    new TextMessageEndEvent { MessageId = "m1" },
    new RunFinishedEvent { ThreadId = "t1", RunId = "r1" }
};

// Serialize for storage
string json = JsonSerializer.Serialize(events, AGUIJsonSerializerContext.Default.Options);
File.WriteAllText("history.json", json);

// Deserialize from storage
string loaded = File.ReadAllText("history.json");
BaseEvent[] restored = JsonSerializer.Deserialize<BaseEvent[]>(loaded, AGUIJsonSerializerContext.Default.Options)!;
```

### Deserialize Events Polymorphically

```csharp
var json = "..."; // SSE data line
var evt = JsonSerializer.Deserialize<BaseEvent>(json, AGUIJsonSerializerContext.Default.Options);

switch (evt)
{
    case TextMessageContentEvent content:
        Console.WriteLine(content.Delta);
        break;
    case RunFinishedEvent finished:
        Console.WriteLine("Run completed");
        break;
}
```

### Create RunAgentInput

```csharp
var input = new RunAgentInput
{
    ThreadId = "thread_123",
    RunId = "run_456",
    Messages = [
        new AGUIUserMessage { Id = "msg_1", Content = "Hello" }
    ],
    Tools = [
        new AGUITool { Name = "get_weather", Description = "Get weather info" }
    ]
};
```

## Troubleshooting

### Serialization Issues

- Ensure all types are registered in `AGUIJsonSerializerContext`
- Check that `JsonPropertyName` attributes use camelCase
- Verify custom converters handle all cases in their Read/Write methods

### AOT Compatibility

- Use source-generated serialization context
- Avoid `JsonSerializer.Deserialize<T>` with generic type parameters at runtime
- Test with `PublishAot=true` to catch AOT issues early

### Event Type Not Recognized

- Verify the `type` property in JSON matches the constant in `AGUIEventTypes`
- Check that `BaseEventJsonConverter.Read` handles the event type
- Ensure the event class is registered in `AGUIJsonSerializerContext`
