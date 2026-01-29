# AGUI.Protocol

A standalone .NET library providing AG-UI (Agent-User Interaction) protocol types including events, messages, tools, and serialization support.

## Overview

AGUI.Protocol provides all the types needed to implement the [AG-UI Protocol](https://docs.ag-ui.com), enabling standardized communication between AI agents and user interfaces using Server-Sent Events (SSE).

## Installation

```bash
dotnet add package AGUI.Protocol
```

## Features

- **24 Event Types**: Complete implementation of all AG-UI protocol events
- **5 Message Types**: User, Assistant, Tool, System, and Developer messages
- **Tool Support**: Types for describing tools and tool calls
- **AOT Compatible**: Source-generated JSON serialization via `AGUIJsonSerializerContext`
- **Multi-target**: Supports net8.0, net9.0, net10.0, netstandard2.0, and net472

## Event Types

### Lifecycle Events
- `RunStartedEvent` - Agent run has started
- `RunFinishedEvent` - Agent run completed successfully
- `RunErrorEvent` - Agent run encountered an error

### Text Message Events
- `TextMessageStartEvent` - Start of a text message
- `TextMessageContentEvent` - Text content chunk
- `TextMessageEndEvent` - End of a text message

### Tool Call Events
- `ToolCallStartEvent` - Tool call initiated
- `ToolCallArgsEvent` - Tool call arguments chunk
- `ToolCallEndEvent` - Tool call completed
- `ToolCallResultEvent` - Tool call result

### State Events
- `StateSnapshotEvent` - Complete state snapshot
- `StateDeltaEvent` - Incremental state update

### Step Events
- `StepStartedEvent` - Step in agent execution started
- `StepFinishedEvent` - Step in agent execution finished

### Activity Events
- `ActivitySnapshotEvent` - Complete activity state snapshot
- `ActivityDeltaEvent` - Incremental activity update

### Messages Event
- `MessagesSnapshotEvent` - Snapshot of conversation messages

### Thinking Events
- `ThinkingStartEvent` - Agent enters thinking phase
- `ThinkingEndEvent` - Agent exits thinking phase
- `ThinkingTextMessageStartEvent` - Start of thinking text
- `ThinkingTextMessageContentEvent` - Thinking text content chunk
- `ThinkingTextMessageEndEvent` - End of thinking text

### Custom Events
- `RawEvent` - Raw event from underlying provider
- `CustomEvent` - Application-defined custom event

## Usage

### Creating Events

```csharp
using AGUI.Protocol;

// Create a text message start event
var startEvent = new TextMessageStartEvent
{
    MessageId = "msg_123",
    Role = AGUIRoles.Assistant
};

// Create a tool call event
var toolCallEvent = new ToolCallStartEvent
{
    ToolCallId = "call_456",
    ToolCallName = "get_weather"
};
```

### Serialization

```csharp
using System.Text.Json;
using AGUI.Protocol;

// Use the source-generated serializer context for AOT compatibility
var options = AGUIJsonSerializerContext.Default.Options;

// Serialize an event
var json = JsonSerializer.Serialize(startEvent, options);

// Deserialize polymorphically
var evt = JsonSerializer.Deserialize<BaseEvent>(json, options);
if (evt is TextMessageStartEvent textStart)
{
    Console.WriteLine($"Message {textStart.MessageId} started");
}
```

### Creating Messages

```csharp
// User message
var userMsg = new AGUIUserMessage
{
    Id = "msg_001",
    Content = "Hello!"
};

// Assistant message with tool calls
var assistantMsg = new AGUIAssistantMessage
{
    Id = "msg_002",
    Content = "",
    ToolCalls =
    [
        new AGUIToolCall
        {
            Id = "call_001",
            Type = "function",
            Function = new AGUIFunctionCall
            {
                Name = "get_weather",
                Arguments = "{\"city\": \"Seattle\"}"
            }
        }
    ]
};

// Tool result message
var toolMsg = new AGUIToolMessage
{
    Id = "msg_003",
    ToolCallId = "call_001",
    Content = "72°F and sunny"
};
```

## Constants

### Event Types
Use `AGUIEventTypes` for event type string constants:
```csharp
AGUIEventTypes.RunStarted      // "RUN_STARTED"
AGUIEventTypes.TextMessageStart // "TEXT_MESSAGE_START"
AGUIEventTypes.ThinkingStart   // "THINKING_START"
// etc.
```

### Roles
Use `AGUIRoles` for message role constants:
```csharp
AGUIRoles.System    // "system"
AGUIRoles.User      // "user"
AGUIRoles.Assistant // "assistant"
AGUIRoles.Developer // "developer"
AGUIRoles.Tool      // "tool"
```

## License

MIT License - Copyright (c) Microsoft Corporation
