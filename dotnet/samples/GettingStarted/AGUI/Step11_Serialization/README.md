# Step 11: Serialization

This sample demonstrates **AG-UI Serialization** - the standard way to persist and restore event streams and sessions.

## Overview

AG-UI serialization has **two distinct dimensions** that work together:

### 1. Client-Side Event Stream Serialization

- Serializes the stream of `BaseEvent` objects for history persistence
- Uses `AGUIJsonSerializerContext` for AOT-compatible JSON serialization
- Enables replay, branching, and compaction of event history

### 2. Server-Side Agent Session Serialization

- Serializes `AgentSession` state using `session.Serialize()` / `agent.DeserializeSessionAsync()`
- Enables conversation persistence across requests/restarts
- Maps to AG-UI `threadId` for session identification

## How They Work Together

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

## Running the Sample

### Prerequisites

1. Set environment variables for Azure OpenAI:
   ```bash
   export AZURE_OPENAI_ENDPOINT=<your-endpoint>
   export AZURE_OPENAI_DEPLOYMENT_NAME=<your-deployment>
   ```

### Start the Server

```bash
cd Server
dotnet run --urls "http://localhost:8888"
```

The server uses an `InMemoryAgentSessionStore` to persist sessions across requests.

### Start the Client

```bash
cd Client
dotnet run
```

### Commands

- Type messages to chat with the assistant
- `:save` - Save all captured events to `history.json`
- `:load` - Load events from `history.json`
- `:q` or `quit` - Exit

## Key Implementation Details

### Server (Program.cs)

```csharp
// Create an in-memory session store
InMemoryAgentSessionStore sessionStore = new();

// Map with session store - enables server-side persistence
app.MapAGUI("/", agent, sessionStore);
```

### Client (Program.cs)

```csharp
// Capture events during streaming
List<BaseEvent> allEvents = [];
await foreach (var update in agent.RunStreamingAsync(messages, session))
{
    if (GetAGUIEvent(update) is BaseEvent evt)
        allEvents.Add(evt);
}

// Serialize events to file
string json = JsonSerializer.Serialize(
    allEvents.ToArray(), 
    AGUIJsonSerializerContext.Default.Options);
File.WriteAllText("history.json", json);

// Deserialize events from file
var restored = JsonSerializer.Deserialize<BaseEvent[]>(
    json, 
    AGUIJsonSerializerContext.Default.Options);
```

## Use Cases

### History Restore
After a page reload or reconnect, clients can restore UI state by replaying saved events.

### Attach to Running Agents
Connect to an existing session and continue receiving events.

### Branching (Time Travel)
Create branches from any prior run using `parentRunId` in `RunStartedEvent`:

```csharp
new RunStartedEvent 
{ 
    ThreadId = "thread_abc", 
    RunId = "run_002",
    ParentRunId = "run_001" // Branch from run_001
}
```

### Server-Side Session Persistence
The server maintains conversation context, so clients only need to send new messages:

```csharp
// First request creates session on server
await agent.RunStreamingAsync([msg1], session, new { ThreadId = threadId });

// Second request - server has prior context
await agent.RunStreamingAsync([msg2], session, new { ThreadId = threadId });
```

## References

- [AG-UI Serialization Spec](https://docs.ag-ui.com/concepts/serialization)
- [AGUI-SERIALIZATION-IMPLEMENTATION-PLAN.md](../../../../../docs/AGUI-SERIALIZATION-IMPLEMENTATION-PLAN.md)
