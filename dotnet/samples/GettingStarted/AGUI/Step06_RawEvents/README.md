# Step 06 - Raw AG-UI Events

This sample demonstrates how to emit and consume AG-UI protocol events directly using the `RawRepresentation` property on `ChatResponseUpdate`/`AgentResponseUpdate`.

## Overview

The AG-UI protocol defines specific event types like `StateSnapshotEvent`, `StateDeltaEvent`, `RunStartedEvent`, etc. While the Microsoft.Agents.AI framework normally converts these to Microsoft.Extensions.AI (M.E.AI) abstractions, this sample shows how you can:

1. **Server-side**: Emit AG-UI events directly by setting `RawRepresentation` on updates
2. **Client-side**: Detect and consume raw AG-UI events by checking `RawRepresentation`

## Key Concepts

### Server-side: Emitting Raw Events

Instead of relying on the framework's conversion from `DataContent` to state events, agents can emit AG-UI events directly:

```csharp
// Create a StateSnapshotEvent directly
var stateSnapshotEvent = new StateSnapshotEvent
{
    Snapshot = JsonSerializer.SerializeToElement(myState)
};

// Emit it via RawRepresentation
yield return new AgentResponseUpdate
{
    RawRepresentation = stateSnapshotEvent,
    Contents = []
};
```

When this flows through `AsAGUIEventStreamAsync`, the framework detects that `RawRepresentation` contains a `BaseEvent` and emits it directly to the SSE stream.

### Client-side: Consuming Raw Events

The `AGUIChatClient` sets `RawRepresentation` on each `ChatResponseUpdate` with the original AG-UI event:

```csharp
await foreach (var update in chatClient.GetStreamingResponseAsync(message))
{
    if (update.RawRepresentation is BaseEvent aguiEvent)
    {
        switch (aguiEvent)
        {
            case StateSnapshotEvent stateSnapshot:
                // Access the raw state snapshot
                var counter = stateSnapshot.Snapshot.GetProperty("counter").GetInt32();
                break;

            case StateDeltaEvent delta:
                // Apply JSON Patch operations
                break;

            // Handle other event types...
        }
    }
}
```

## Running the Sample

1. **Start the server**:
   ```bash
   cd Server
   dotnet run
   ```

2. **In a separate terminal, run the client**:
   ```bash
   cd Client
   dotnet run
   ```

## The Counter Agent

The sample includes a `CounterAgent` that:
- Maintains a simple counter state
- Responds to commands like "increment", "decrement", "reset"
- Emits `StateSnapshotEvent` directly when state changes
- Delegates text generation to an inner chat client

## Benefits of Raw Event Access

1. **Type Safety**: Work with strongly-typed AG-UI event classes
2. **Full Protocol Access**: Access all AG-UI event properties, not just M.E.AI abstractions
3. **Selective Handling**: Handle specific event types differently
4. **Interoperability**: Easier integration with other AG-UI clients/servers
