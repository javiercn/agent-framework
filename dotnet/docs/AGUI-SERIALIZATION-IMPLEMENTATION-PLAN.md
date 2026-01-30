# AG-UI Serialization Implementation Plan

This document outlines the implementation plan for adding **Serialization** support to the .NET AG-UI libraries, based on the [AG-UI Serialization specification](https://docs.ag-ui.com/concepts/serialization).

## Overview

AG-UI Serialization provides a standard way to persist and restore event streams for:
- **History restore**: Restore chat history and UI state after reloads/reconnects
- **Attach to running agents**: Continue receiving events from existing sessions
- **Branching (time travel)**: Create branches from any prior run
- **Event compaction**: Reduce storage size while preserving semantics

## Key Concepts

### Two Serialization Dimensions

AG-UI serialization has **two distinct dimensions** that work together:

1. **Client-Side Event Stream Serialization** (AG-UI Protocol)
   - Serializes the stream of `BaseEvent` objects for history persistence
   - Uses `AGUIJsonSerializerContext` for AOT-compatible JSON serialization
   - Enables replay, branching, and compaction of event history

2. **Server-Side Agent Session Serialization** (.NET Agent Framework)
   - Serializes `AgentSession` state using `session.Serialize()` / `agent.DeserializeSessionAsync()`
   - Enables conversation persistence across requests/restarts
   - Maps to AG-UI `threadId` for session identification

### How They Work Together

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           AG-UI CLIENT                                   │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │ Event Stream (BaseEvent[])                                       │   │
│  │ - RunStartedEvent, TextMessageContentEvent, ...                  │   │
│  │ - Serialized for history replay/branching                        │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                              ↕ HTTP/SSE                                 │
│                          threadId                                       │
└─────────────────────────────────────────────────────────────────────────┘
                               ↕
┌─────────────────────────────────────────────────────────────────────────┐
│                          AG-UI SERVER                                    │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │ AgentSession (per threadId)                                      │   │
│  │ - ChatHistoryProvider (conversation history)                     │   │
│  │ - Agent-specific state                                           │   │
│  │ - Serialized via session.Serialize() → JsonElement               │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

## Current State Analysis

### Existing Infrastructure

The .NET AG-UI implementation already has:

| Component | Location | Status |
|-----------|----------|--------|
| `BaseEvent` and all event types | `src/AGUI.Protocol/Events/` | ✅ Complete |
| `AGUIJsonSerializerContext` | `src/AGUI.Protocol/Serialization/` | ✅ Complete |
| `MessagesSnapshotEvent` | `src/AGUI.Protocol/Events/` | ✅ Complete |
| `RunStartedEvent` | `src/AGUI.Protocol/Events/` | ✅ Complete (has `parentRunId` and `input` fields) |
| `RunAgentInput` | `src/AGUI.Protocol/Context/` | ✅ Complete (has `parentRunId` for branching) |
| SSE Encoding/Decoding | `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | ✅ Complete (handled by hosting layer) |
| Client Event Stream Serialization | `System.Text.Json` | ✅ Complete (via `JsonSerializer.Serialize/Deserialize<BaseEvent[]>`) |
| `AgentSession.Serialize()` | `Microsoft.Agents.AI.Abstractions` | ✅ Complete |
| `AIAgent.DeserializeSessionAsync()` | `Microsoft.Agents.AI.Abstractions` | ✅ Complete |
| Server Session Store | `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | ✅ Complete (integrated via `MapAGUI` overload) |

### Gap Analysis

Based on the AG-UI spec (see [types.mdx](https://github.com/ag-ui-protocol/ag-ui/blob/main/docs/sdk/js/core/types.mdx) and [serialization.mdx](https://github.com/ag-ui-protocol/ag-ui/blob/main/docs/concepts/serialization.mdx)):

**✅ All required serialization features are implemented:**

1. **`RunStartedEvent`** - Has `parentRunId` and `input` fields for branching/time travel
2. **`RunAgentInput`** - Has `parentRunId` field for client-initiated branching (added per AG-UI spec)
3. **Client Event Stream Serialization** - `JsonSerializer.Serialize/Deserialize<BaseEvent[]>` works correctly
4. **Server Session Store** - Integrated via `MapAGUI(pattern, agent, sessionStore)` overload

### Already Complete

- **SSE Encoding/Decoding** - Handled by `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` (server-side) and `Microsoft.Agents.AI.AGUI` (client-side via `System.Net.ServerSentEvents`)
- **Client Event Stream Serialization** - Available via `JsonSerializer` with `AGUIJsonSerializerContext.Default.Options`
- **AgentSession Serialization** - `AgentSession.Serialize()` returns `JsonElement`, `AIAgent.DeserializeSessionAsync()` restores it (see [Agent_Step06_PersistedConversations](../samples/GettingStarted/Agents/Agent_Step06_PersistedConversations/Program.cs))

### Out of Scope (Future Work)

- **Event Compaction** (`compactEvents`) - Reduce verbose streams to snapshots. This is an optimization that can be added later when there is a concrete need for history persistence with reduced storage.

---

## Implementation Tasks

### Phase 1: Core Protocol Updates

#### Task 1.1: Update `RunStartedEvent` with Lineage Fields

**File**: `src/AGUI.Protocol/Events/RunStartedEvent.cs`

Add the following fields per AG-UI spec:

```csharp
/// <summary>
/// Gets or sets the parent run identifier for branching/time travel.
/// </summary>
/// <remarks>
/// If present, refers to a prior run within the same thread, creating a git-like append-only log.
/// </remarks>
[JsonPropertyName("parentRunId")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? ParentRunId { get; set; }

/// <summary>
/// Gets or sets the exact agent input payload for this run.
/// </summary>
/// <remarks>
/// May omit messages already present in history; compactEvents() will normalize.
/// </remarks>
[JsonPropertyName("input")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public RunAgentInput? Input { get; set; }
```

**Tests**: Update `EventSerializationTests.cs` to verify new fields.

---

### Phase 2: Verify Client Event Stream Serialization

#### Task 2.1: Verify `BaseEvent[]` Serialization Round-Trip

The existing `AGUIJsonSerializerContext` should already support serializing and deserializing arrays of events. We need to verify this works correctly:

```csharp
// Serialize events to JSON for storage
var events = new BaseEvent[] { runStartedEvent, textMessageStartEvent, /* ... */ };
string json = JsonSerializer.Serialize(events, AGUIJsonSerializerContext.Default.Options);

// Deserialize events from stored JSON
BaseEvent[] restored = JsonSerializer.Deserialize<BaseEvent[]>(json, AGUIJsonSerializerContext.Default.Options);
```

**Verification points**:
- Polymorphic deserialization works (correct derived types are restored)
- All event properties round-trip correctly
- Null/optional fields are handled properly

---

### Phase 3: Server Session Store Integration

#### Background: Current Server Behavior

The current AGUI hosting layer (`AGUIEndpointRouteBuilderExtensions.MapAGUI`) does **not** persist `AgentSession` state between requests. Each request:
1. Receives `RunAgentInput` with a `threadId`
2. Creates messages from the input
3. Runs the agent **without** an existing session
4. The agent creates a new session internally each time

This means the server is **stateless** - all conversation history must come from the client via `RunAgentInput.messages`.

#### Existing Infrastructure: How AgentSessionStore Works

The `AgentSessionStore` is an abstract class with two key methods:

```csharp
public abstract class AgentSessionStore
{
    // Retrieves or creates a session for the given conversationId
    public abstract ValueTask<AgentSession> GetSessionAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken = default);

    // Saves a session after the agent run
    public abstract ValueTask SaveSessionAsync(
        AIAgent agent,
        string conversationId,
        AgentSession session,
        CancellationToken cancellationToken = default);
}
```

**Built-in implementations**:

| Implementation | Behavior |
|----------------|----------|
| `InMemoryAgentSessionStore` | Stores sessions in a `ConcurrentDictionary<string, JsonElement>`. Returns existing session if found, or creates new via `agent.GetNewSessionAsync()`. |
| `NoopAgentSessionStore` | Always creates a new session (stateless). Does not persist anything. |

**Session lookup logic** (from `InMemoryAgentSessionStore`):
```csharp
public override async ValueTask<AgentSession> GetSessionAsync(AIAgent agent, string conversationId, CancellationToken cancellationToken)
{
    var key = GetKey(conversationId, agent.Id);
    JsonElement? sessionContent = this._threads.TryGetValue(key, out var existingSession) ? existingSession : null;

    return sessionContent switch
    {
        null => await agent.GetNewSessionAsync(cancellationToken).ConfigureAwait(false),
        _ => await agent.DeserializeSessionAsync(sessionContent.Value, cancellationToken: cancellationToken).ConfigureAwait(false),
    };
}
```

**Key insight**: The session retrieval is delegated to the `AgentSessionStore` implementation. Users can provide their own implementation (e.g., Redis, Cosmos DB) via `builder.WithSessionStore()`.

#### Compatibility with Azure AI Foundry Agents

The `AgentSessionStore` approach is **agnostic to the underlying AI service** because it delegates to each agent's serialization logic. This works correctly with Azure AI Foundry Agents (Persistent Agents):

| Session Mode | How It Works | Serialized State |
|--------------|--------------|------------------|
| **Server-managed** (Foundry Agents) | Service stores conversation history server-side. Session stores `ConversationId` (thread ID). | `{"ConversationId": "thread_abc123"}` |
| **Client-managed** (OpenAI Chat, etc.) | Messages stored locally via `ChatHistoryProvider`. | `{"ChatHistoryProviderState": {...messages...}}` |

**Flow for Foundry Agents**:
1. **New conversation**: `AgentSessionStore.GetSessionAsync` → no stored session → `agent.GetNewSessionAsync()` → empty session
2. **First message**: Agent runs, Foundry creates thread, returns `ConversationId` → session updated with thread ID
3. **Session save**: `session.Serialize()` → `{"ConversationId": "thread_abc123"}`
4. **Session restore**: `agent.DeserializeSessionAsync(json)` → session with `ConversationId` restored
5. **Subsequent messages**: Agent uses existing `ConversationId` → Foundry continues the thread

The `ChatClientAgentSession.DeserializeAsync` method correctly handles both modes:
```csharp
if (state?.ConversationId is string sessionId)
{
    session.ConversationId = sessionId;
    // Server-managed: return session with just the ID
    return session;
}
// Client-managed: restore ChatHistoryProvider with messages
session._chatHistoryProvider = await chatHistoryProviderFactory.Invoke(...);
```

#### Reference: A2A Hosting Pattern

The A2A hosting already uses session persistence via `AIHostAgent`:

```csharp
// From AIAgentExtensions.cs in Microsoft.Agents.AI.Hosting.A2A
async Task<A2AResponse> OnMessageReceivedAsync(MessageSendParams messageSendParams, CancellationToken cancellationToken)
{
    var contextId = messageSendParams.Message.ContextId ?? Guid.NewGuid().ToString("N");
    
    // Get or create session using threadId (contextId in A2A)
    var session = await hostAgent.GetOrCreateSessionAsync(contextId, cancellationToken).ConfigureAwait(false);
    
    var response = await hostAgent.RunAsync(
        messageSendParams.ToChatMessages(),
        session: session,
        options: options,
        cancellationToken: cancellationToken).ConfigureAwait(false);
    
    // Save session after run completes
    await hostAgent.SaveSessionAsync(contextId, session, cancellationToken).ConfigureAwait(false);
    
    return /* response */;
}
```

#### Task 3.1: Add Session Store Support to AGUI MapAGUI

**File**: `src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIEndpointRouteBuilderExtensions.cs`

Add an overload that accepts an `AgentSessionStore`:

```csharp
/// <summary>
/// Maps an AG-UI agent endpoint with session persistence.
/// </summary>
public static IEndpointConventionBuilder MapAGUI(
    this IEndpointRouteBuilder endpoints,
    [StringSyntax("route")] string pattern,
    AIAgent aiAgent,
    AgentSessionStore sessionStore)
{
    return endpoints.MapPost(pattern, async ([FromBody] RunAgentInput? input, HttpContext context, CancellationToken cancellationToken) =>
    {
        if (input is null)
        {
            return Results.BadRequest();
        }

        var jsonOptions = context.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
        var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

        var messages = input.Messages.AsChatMessages(jsonSerializerOptions).ToList();
        var clientTools = input.Tools?.AsAITools().ToList();

        // Handle resume payload if present (continuing from an interrupt)
        if (input.Resume is { } resume)
        {
            var resumeContent = InterruptContentExtensions.FromAGUIResume(resume);
            messages.Add(new ChatMessage(ChatRole.User, [resumeContent]));
        }

        // Get or create session based on threadId
        var session = await sessionStore.GetSessionAsync(aiAgent, input.ThreadId, cancellationToken);

        var runOptions = new ChatClientAgentRunOptions
        {
            ChatOptions = new ChatOptions
            {
                Tools = clientTools,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["ag_ui_state"] = input.State,
                    ["ag_ui_context"] = input.Context?.Select(c => new KeyValuePair<string, string>(c.Description, c.Value)).ToArray(),
                    ["ag_ui_forwarded_properties"] = input.ForwardedProperties,
                    ["ag_ui_thread_id"] = input.ThreadId,
                    ["ag_ui_run_id"] = input.RunId
                }
            }
        };

        // Run the agent with session
        var events = aiAgent.RunStreamingAsync(
            messages,
            session: session,
            options: runOptions,
            cancellationToken: cancellationToken)
            .AsChatResponseUpdatesAsync()
            .FilterServerToolsFromMixedToolInvocationsAsync(clientTools, cancellationToken)
            .AsAGUIEventStreamAsync(
                input.ThreadId,
                input.RunId,
                jsonSerializerOptions,
                cancellationToken);

        // Save session after streaming completes
        // Note: For streaming, we need a callback after the response is sent
        context.Response.OnCompleted(async () =>
        {
            await sessionStore.SaveSessionAsync(aiAgent, input.ThreadId, session, CancellationToken.None);
        });

        var sseLogger = context.RequestServices.GetRequiredService<ILogger<AGUIServerSentEventsResult>>();
        return new AGUIServerSentEventsResult(events, sseLogger);
    });
}
```

#### Task 3.2: Add DI-Based Registration

**File**: `src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIHostedAgentBuilderExtensions.cs` (new file)

```csharp
/// <summary>
/// Extension methods for configuring AG-UI agent hosting with session persistence.
/// </summary>
public static class AGUIHostedAgentBuilderExtensions
{
    /// <summary>
    /// Configures the agent to use the specified session store for AG-UI hosting.
    /// </summary>
    public static IHostedAgentBuilder WithAGUISessionStore(this IHostedAgentBuilder builder, AgentSessionStore store)
    {
        builder.ServiceCollection.AddKeyedSingleton<AgentSessionStore>($"agui:{builder.Name}", store);
        return builder;
    }

    /// <summary>
    /// Configures the agent to use an in-memory session store for AG-UI hosting.
    /// </summary>
    public static IHostedAgentBuilder WithAGUIInMemorySessionStore(this IHostedAgentBuilder builder)
    {
        return builder.WithAGUISessionStore(new InMemoryAgentSessionStore());
    }
}
```

#### Task 3.3: Considerations for Streaming

Unlike the A2A synchronous pattern, AG-UI uses SSE streaming. This creates a challenge:
- Session state changes during the streaming run
- We need to save the session **after** the stream completes
- Using `Response.OnCompleted` ensures we save after all events are sent

**Alternative**: Use a `SessionPersistingAsyncEnumerable<T>` wrapper that saves the session when the enumeration completes.

---

### Phase 4: Documentation Updates

#### Task 4.1: Update AGENTS.md

Add a section to `src/AGUI.Protocol/AGENTS.md` documenting the serialization pattern.

---

## Unit Tests

### Test File: `tests/AGUI.Protocol.UnitTests/EventArraySerializationTests.cs`

```csharp
public class EventArraySerializationTests
{
    [Fact]
    public void SerializeDeserialize_EventArray_RoundTripsCorrectly();
    
    [Fact]
    public void SerializeDeserialize_MixedEventTypes_PreservesPolymorphism();
    
    [Fact]
    public void SerializeDeserialize_EmptyArray_ReturnsEmpty();
    
    [Fact]
    public void SerializeDeserialize_AllEventTypes_RoundTrip();
    
    [Fact]
    public void SerializeDeserialize_WithNullableFields_HandlesCorrectly();
}
```

### Test File: `tests/AGUI.Protocol.UnitTests/RunStartedEventLineageTests.cs`

```csharp
public class RunStartedEventLineageTests
{
    [Fact]
    public void ParentRunId_SerializesWhenSet();
    
    [Fact]
    public void ParentRunId_OmittedWhenNull();
    
    [Fact]
    public void Input_SerializesWhenSet();
    
    [Fact]
    public void Input_OmittedWhenNull();
    
    [Fact]
    public void BranchingScenario_PreservesLineage();
}
```

### Test File: `tests/AGUI.Protocol.UnitTests/RunAgentInputSerializationTests.cs` (NEW)

The `RunAgentInput` type includes a `parentRunId` field that clients use to indicate branching/time-travel.
This is the HTTP POST body sent TO the server, which is separate from `RunStartedEvent.ParentRunId` 
that the server emits back in the event stream.

```csharp
public class RunAgentInputSerializationTests
{
    [Fact]
    public void ParentRunId_SerializesWhenSet();
    
    [Fact]
    public void ParentRunId_OmittedWhenNull();
    
    [Fact]
    public void ParentRunId_DeserializesFromClientJson();
    
    [Fact]
    public void BranchingScenario_ClientSendsParentRunId();
    
    [Fact]
    public void FullInput_WithAllFields_RoundTripsCorrectly();
}
```

---

## Integration Tests

### Test File: `tests/AGUI.Protocol.UnitTests/EventArraySerializationTests.cs` (continued)

```csharp
// Additional integration-style tests in the same file
[Fact]
public void FullConversation_SerializeDeserializeRoundTrip();

[Fact]
public void BranchingScenario_WithParentRunId_PreservesLineage();
```

### Test File: `tests/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests/SessionPersistenceTests.cs`

```csharp
public class SessionPersistenceTests
{
    [Fact]
    public async Task MapAGUI_WithSessionStore_PersistsSessionAcrossRequests();
    
    [Fact]
    public async Task MapAGUI_WithSessionStore_RestoresSessionFromThreadId();
    
    [Fact]
    public async Task MapAGUI_WithInMemorySessionStore_SessionContainsChatHistory();
    
    [Fact]
    public async Task MapAGUI_WithNoopSessionStore_AlwaysCreatesNewSession();
    
    [Fact]
    public async Task MapAGUI_NewThreadId_CreatesNewSession();
    
    [Fact]
    public async Task MapAGUI_ExistingThreadId_ReusesSession();
}
```

### Test File: `tests/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests/BranchingTests.cs` (NEW)

Integration tests that verify the full branching/time-travel flow where the client sends `parentRunId`
in the HTTP POST body and receives it back in the `RunStartedEvent`.

```csharp
public class BranchingTests
{
    [Fact]
    public async Task MapAGUI_ClientSendsParentRunId_ServerReceivesIt();
    
    [Fact]
    public async Task MapAGUI_BranchingScenario_RunStartedEventIncludesParentRunId();
    
    [Fact]
    public async Task MapAGUI_BranchingWithResume_WorksCorrectly();
}
```

---

## Sample Application

### Sample: `samples/GettingStarted/AGUI/Step11_Serialization/`

Create a sample demonstrating **both** client event stream serialization **and** server session persistence:

1. **Server** (`Server/Program.cs`):
   - Agent with `InMemoryAgentSessionStore` for session persistence
   - Demonstrates `MapAGUI` overload with session store
   - Endpoint to retrieve event history as JSON

2. **Client** (`Client/Program.cs`):
   - Connects to server
   - Sends messages across multiple requests (same `threadId`)
   - Saves event stream locally using `JsonSerializer`
   - Demonstrates session continuity

#### Server Structure:

```
Step11_Serialization/
├── Server/
│   ├── Server.csproj
│   ├── Program.cs           # Agent with session store
│   └── EventStore.cs        # In-memory event store (optional)
├── Client/
│   ├── Client.csproj
│   └── Program.cs           # CLI demonstrating serialization + session continuity
└── README.md                # Sample documentation
```

#### Sample Server Code (conceptual):

```csharp
// Program.cs
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddAGUI();

// Add session store for session persistence
builder.Services.AddSingleton<AgentSessionStore>(new InMemoryAgentSessionStore());

WebApplication app = builder.Build();

AIAgent agent = /* create agent */;
var sessionStore = app.Services.GetRequiredService<AgentSessionStore>();

// Map with session store - server now maintains conversation state
app.MapAGUI("/", agent, sessionStore);

await app.RunAsync();
```

#### Sample Client Code (conceptual):

```csharp
// Program.cs
using var client = new AGUIChatClient(new Uri("http://localhost:8888/"));

string threadId = Guid.NewGuid().ToString(); // Shared across requests
List<BaseEvent> allEvents = [];

// First message
await foreach (var update in client.GetStreamingResponseAsync(
    [new ChatMessage(ChatRole.User, "Hello!")],
    new AGUIChatOptions { ThreadId = threadId }))
{
    if (update.RawRepresentation is BaseEvent evt)
        allEvents.Add(evt);
}

// Second message - server remembers context via session store
await foreach (var update in client.GetStreamingResponseAsync(
    [new ChatMessage(ChatRole.User, "What did I just say?")], // Only new message needed!
    new AGUIChatOptions { ThreadId = threadId }))
{
    if (update.RawRepresentation is BaseEvent evt)
        allEvents.Add(evt);
}

// Save to file
string json = JsonSerializer.Serialize(allEvents.ToArray(), AGUIJsonSerializerContext.Default.Options);
File.WriteAllText("history.json", json);

Console.WriteLine($"Saved {allEvents.Count} events to history.json");

// Load from file
string loaded = File.ReadAllText("history.json");
var restored = JsonSerializer.Deserialize<BaseEvent[]>(loaded, AGUIJsonSerializerContext.Default.Options);
Console.WriteLine($"Loaded {restored?.Length ?? 0} events from history.json");
```

---

## Implementation Order

| Order | Task | Dependency | Estimated Effort | Status |
|-------|------|------------|------------------|--------|
| 1 | Update `RunStartedEvent` with `parentRunId` and `input` | None | Small | ✅ Done |
| 2 | Update `RunAgentInput` with `parentRunId` | None | Small | ✅ Done |
| 3 | Unit Tests (`RunStartedEventLineageTests.cs`) | Task 1 | Small | ✅ Done |
| 4 | Unit Tests (`EventArraySerializationTests.cs`) | Task 1 | Small | ✅ Done |
| 5 | Unit Tests (`RunAgentInputSerializationTests.cs`) | Task 2 | Small | ✅ Done |
| 6 | Add `MapAGUI` overload with `AgentSessionStore` | Task 1-4 | Medium | ✅ Done |
| 7 | Add DI-based session store registration | Task 6 | Small | ✅ Done |
| 8 | Integration tests (`SessionPersistenceTests.cs`) | Task 6, 7 | Medium | ✅ Done |
| 9 | Integration tests (`BranchingTests.cs`) | Task 2, 6 | Medium | ✅ Done |
| 10 | Sample Application (`Step11_Serialization`) | Task 6-8 | Medium | ✅ Done |
| 11 | Update AGENTS.md documentation | All above | Small | ✅ Done |

### Future Work (Not In Scope)

| Task | Description | When to Implement |
|------|-------------|-------------------|
| Event Compaction | Reduce event stream size while preserving semantics | When storage optimization is needed |

---

## API Surface Summary

### New Public Types

None - all functionality is provided by existing infrastructure.

### Updated Types

| Type | Changes |
|------|---------|
| `RunStartedEvent` | Add `ParentRunId` and `Input` properties (server emits in event stream) |
| `RunAgentInput` | Add `ParentRunId` property (client sends in HTTP POST body for branching) |

### Existing Types Used

| Type | Purpose |
|------|---------|
| `AGUIJsonSerializerContext.Default.Options` | JSON serialization options for all AG-UI types |
| `JsonSerializer.Serialize<BaseEvent[]>` | Serialize event arrays for persistence |
| `JsonSerializer.Deserialize<BaseEvent[]>` | Deserialize event arrays from storage |
| `AgentSession.Serialize()` | Serialize server-side session state to `JsonElement` |
| `AIAgent.DeserializeSessionAsync()` | Restore session from serialized `JsonElement` |

---

## Server-Side Session Management

### Relationship to AG-UI threadId

| AG-UI Concept | .NET Concept | Mapping |
|---------------|--------------|---------|
| `threadId` | `conversationId` in `AgentSessionStore` | Used as storage key for `AgentSession` |
| `runId` | Individual request | Not persisted (each run is independent) |
| `parentRunId` | Branching | Future: Could create new session based on parent |
| `input.messages` | Conversation history | Merged with session-stored history when using server state |

### Design Decision: Client vs Server State

AG-UI supports two approaches, and **this implementation supports both**:

1. **Client-Managed State** (Default - using existing `MapAGUI` overload)
   - Client sends full `messages` array with each request
   - Server is stateless, processes messages and returns events
   - Simpler server, client controls history
   - Use when: Browser-based clients that maintain their own state

2. **Server-Managed State** (New - using `MapAGUI` with `AgentSessionStore`)
   - Server persists `AgentSession` keyed by `threadId`
   - Client can send only new messages (server has history)
   - Server maintains authoritative conversation state
   - Use when: Multiple clients, server-side persistence required, thin clients

### AgentSession Serialization Pattern

The `AgentSessionStore` abstracts the storage mechanism:

```csharp
// Built-in implementations:
// - InMemoryAgentSessionStore: ConcurrentDictionary (dev/testing)
// - NoopAgentSessionStore: Always creates new session (stateless)
// - Custom: Implement AgentSessionStore for Redis, Cosmos DB, SQL, etc.

// How it works:
// 1. GetSessionAsync checks storage for existing session
// 2. If found: deserializes via agent.DeserializeSessionAsync(jsonElement)
// 3. If not found: creates new via agent.GetNewSessionAsync()
// 4. SaveSessionAsync serializes via session.Serialize() and stores
```

See [Agent_Step06_PersistedConversations](../samples/GettingStarted/Agents/Agent_Step06_PersistedConversations/Program.cs) for a complete example of the underlying pattern.

---

## Compatibility Notes

- All types support AOT compilation via `AGUIJsonSerializerContext`
- JSON format is compatible with TypeScript/Python SDKs
- No breaking changes to existing APIs

## Out of Scope (Future Work)

The following features from the AG-UI spec are not included in this implementation:

### Event Compaction

The TypeScript SDK provides a `compactEvents` function that:
- Combines `TEXT_MESSAGE_*` sequences into single deltas
- Collapses `TOOL_CALL_*` sequences into single args deltas
- Reorders interleaved events to keep streaming blocks contiguous
- Merges consecutive `STATE_DELTA` into final `STATE_SNAPSHOT`

This can be added later when there is a concrete need for optimized history storage. Reference: [TypeScript compactEvents](https://github.com/ag-ui-protocol/ag-ui/blob/main/sdks/typescript/packages/client/src/compact/compact.ts)

---

## References

- [AG-UI Serialization Spec](https://docs.ag-ui.com/concepts/serialization)
- [TypeScript compactEvents](https://github.com/ag-ui-protocol/ag-ui/blob/main/sdks/typescript/packages/client/src/compact/compact.ts)
- [Python EventEncoder](https://github.com/ag-ui-protocol/ag-ui/blob/main/sdks/python/ag_ui/encoder/encoder.py)
- [Go Encoding Package](https://github.com/ag-ui-protocol/ag-ui/tree/main/sdks/community/go/pkg/encoding)
