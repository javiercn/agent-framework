# AG-UI Improvements Plan for Microsoft Agent Framework (.NET)

## Overview

This document outlines the planned improvements for the AG-UI (Agent-User Interaction) protocol implementation in the Microsoft Agent Framework .NET SDK. The AG-UI protocol is an open, lightweight, event-based protocol that standardizes how AI agents connect to user-facing applications.

---

## Why a Separate Protocol Package?

The AG-UI protocol types are currently internal and duplicated across packages via linked `Shared/` folders. This creates several problems:

1. **Users cannot emit AG-UI events directly** - Agents, tools, and middleware cannot yield custom AG-UI events (like `StepStartedEvent` or `CustomEvent`) because the types are internal.

2. **Users cannot inspect original events on the client** - The client maps AG-UI events to `Microsoft.Extensions.AI` types, but users have no way to access the original AG-UI event to handle protocol-specific features.

3. **Missing event types block feature adoption** - Without public event type definitions, users cannot implement support for missing events (STEP_*, ACTIVITY_*, THINKING_*, etc.) themselves.

4. **Protocol types should be framework-agnostic** - The AG-UI protocol is an open standard. The protocol types package (`AGUI`) should have no dependencies on Microsoft.Extensions.AI or ASP.NET Core, allowing it to be used by any .NET application.

By creating a standalone `AGUI` package with public protocol types:
- **Server-side**: Agents can emit any AG-UI event via `RawRepresentation`, and the hosting layer will serialize it to SSE.
- **Client-side**: Users can inspect `RawRepresentation` to access the original AG-UI event, even for events with M.E.AI mappings.
- **Extensibility**: Users can implement support for missing events without waiting for framework updates.

---

## Table of Contents

1. [Package Restructuring](#1-package-restructuring)
2. [Public AG-UI Event Types](#2-public-ag-ui-event-types)
3. [Server-Side Event Emission](#3-server-side-event-emission)
4. [Client-Side Event Handling](#4-client-side-event-handling)
5. [Missing AG-UI Events](#5-missing-ag-ui-events)
6. [Workflow Support](#6-workflow-support)
7. [Interruptions](#7-interruptions)
8. [Multi-Modal Inputs](#8-multi-modal-inputs)
9. [Bug Fixes & Improvements](#9-bug-fixes--improvements)
10. [Active Pull Requests](#10-active-pull-requests)

---

## 1. Package Restructuring

### Current Structure

```
Microsoft.Agents.AI.AGUI
├── AGUIChatClient.cs (client implementation)
├── AGUIHttpService.cs
└── Shared/ (internal protocol types)

Microsoft.Agents.AI.Hosting.AGUI.AspNetCore
├── AGUIEndpointRouteBuilderExtensions.cs
├── AGUIServerSentEventsResult.cs
└── Shared/ (linked, internal protocol types)
```

**Problems:**
- Protocol types are `internal` in a `Shared` folder
- Users cannot emit AG-UI events directly
- Users cannot inspect original AG-UI events on the client side
- Client and protocol are tightly coupled

### Proposed Structure

```
AGUI                                        [NEW - Protocol Types]
├── Events/
│   ├── BaseEvent.cs
│   ├── RunStartedEvent.cs
│   ├── RunFinishedEvent.cs
│   ├── RunErrorEvent.cs
│   ├── TextMessageStartEvent.cs
│   ├── TextMessageContentEvent.cs
│   ├── TextMessageEndEvent.cs
│   ├── ToolCallStartEvent.cs
│   ├── ToolCallArgsEvent.cs
│   ├── ToolCallEndEvent.cs
│   ├── ToolCallResultEvent.cs
│   ├── StateSnapshotEvent.cs
│   ├── StateDeltaEvent.cs
│   ├── MessagesSnapshotEvent.cs
│   ├── StepStartedEvent.cs
│   ├── StepFinishedEvent.cs
│   ├── ActivitySnapshotEvent.cs
│   ├── ActivityDeltaEvent.cs
│   ├── ThinkingTextMessageStartEvent.cs
│   ├── ThinkingTextMessageContentEvent.cs
│   ├── ThinkingTextMessageEndEvent.cs
│   ├── CustomEvent.cs
│   └── RawEvent.cs
├── Messages/
│   ├── AGUIMessage.cs
│   ├── AGUIUserMessage.cs
│   ├── AGUIAssistantMessage.cs
│   ├── AGUIToolMessage.cs
│   ├── AGUISystemMessage.cs
│   └── AGUIDeveloperMessage.cs
├── AGUIEventTypes.cs (constants)
├── AGUIRoles.cs
├── AGUITool.cs
├── AGUIContextItem.cs
├── RunAgentInput.cs
└── AGUIJsonSerializerContext.cs

Microsoft.Agents.AI.AGUI                    [EXISTING - Client Package]
├── AGUIChatClient.cs
├── AGUIHttpService.cs
└── (references AGUI for protocol types)

Microsoft.Agents.AI.Hosting.AGUI.AspNetCore [EXISTING - Server Hosting]
├── AGUIEndpointRouteBuilderExtensions.cs
├── AGUIServerSentEventsResult.cs
├── ServiceCollectionExtensions.cs
└── (references AGUI for protocol types)
```

### Migration Path

1. Create new `AGUI` package with public protocol types
2. Move `Shared/` types to new package and make public
3. Update `Microsoft.Agents.AI.AGUI` (client) to reference protocol package
4. Update `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` to reference protocol package
5. Mark old internal types as `[Obsolete]` for one release cycle

---

## 2. Public AG-UI Event Types

### Goal

Make all AG-UI event types public so users can:
- Create and emit custom events from agents
- Inspect original events on the client side
- Extend the protocol with custom event types

### API Design

```csharp
namespace AGUI;

/// <summary>
/// Base class for all AG-UI protocol events.
/// </summary>
public abstract class BaseEvent
{
    /// <summary>
    /// The event type identifier (e.g., "RUN_STARTED", "TEXT_MESSAGE_CONTENT").
    /// </summary>
    public abstract string Type { get; }
    
    /// <summary>
    /// Optional timestamp for the event.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }
}

/// <summary>
/// Signals the start of an agent run.
/// </summary>
public sealed class RunStartedEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.RunStarted;
    public required string ThreadId { get; set; }
    public required string RunId { get; set; }
}

/// <summary>
/// Delivers streaming text content.
/// </summary>
public sealed class TextMessageContentEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.TextMessageContent;
    public required string MessageId { get; set; }
    public required string Delta { get; set; }
}

/// <summary>
/// Provides a snapshot of all messages in the conversation.
/// </summary>
public sealed class MessagesSnapshotEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.MessagesSnapshot;
    public required IReadOnlyList<AGUIMessage> Messages { get; set; }
}

/// <summary>
/// Signals the start of a step within an agent run.
/// </summary>
public sealed class StepStartedEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.StepStarted;
    public required string StepId { get; set; }
    public required string StepName { get; set; }
    public string? ParentStepId { get; set; }
}

/// <summary>
/// Signals the completion of a step.
/// </summary>
public sealed class StepFinishedEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.StepFinished;
    public required string StepId { get; set; }
}

/// <summary>
/// Custom application-specific event.
/// </summary>
public sealed class CustomEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.Custom;
    public required string Name { get; set; }
    public JsonElement? Value { get; set; }
}
```

---

## 3. Server-Side Event Emission

### Goal

Allow agents, tools, and middleware to emit AG-UI events directly to the response stream.

### API Design: Via RawRepresentation

```csharp
// In agent or tool code
yield return new AgentRunResponseUpdate
{
    RawRepresentation = new StepStartedEvent
    {
        StepId = "step_1",
        StepName = "Analyzing request"
    }
};

// In AsAGUIEventStreamAsync - detect and pass through
if (update.RawRepresentation is BaseEvent aguiEvent)
{
    yield return aguiEvent;
}
```

### Server-Side Conversion

```csharp
// In AsAGUIEventStreamAsync
public static async IAsyncEnumerable<BaseEvent> AsAGUIEventStreamAsync(
    this IAsyncEnumerable<ChatResponseUpdate> updates,
    string threadId,
    string runId,
    JsonSerializerOptions jsonSerializerOptions,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    yield return new RunStartedEvent { ThreadId = threadId, RunId = runId };

    await foreach (var update in updates.WithCancellation(cancellationToken))
    {
        // Check for direct AG-UI event emission
        if (update.RawRepresentation is BaseEvent directEvent)
        {
            yield return directEvent;
            continue;
        }
        
        // Existing mapping logic for TextContent, FunctionCallContent, etc.
        // ...
    }

    yield return new RunFinishedEvent { ThreadId = threadId, RunId = runId };
}
```

---

## 4. Client-Side Event Handling

### Goal

When the AG-UI client receives events, always preserve the original AG-UI event in `RawRepresentation` so users can inspect and handle it, even when M.E.AI mappings exist.

### API Design

```csharp
// ChatResponseUpdate with original AG-UI event always preserved
var update = new ChatResponseUpdate
{
    // Standard M.E.AI properties populated where possible
    Role = ChatRole.Assistant,
    Contents = [...],
    
    // Original AG-UI event ALWAYS preserved in RawRepresentation
    RawRepresentation = originalAguiEvent
};
```

### Event Mapping Strategy

All events preserve `RawRepresentation` regardless of whether they have M.E.AI mappings:

| AG-UI Event | M.E.AI Mapping | RawRepresentation |
|-------------|----------------|-------------------|
| `RUN_STARTED` | Empty update with ConversationId/ResponseId | ✅ Preserved |
| `RUN_FINISHED` | Empty update | ✅ Preserved |
| `RUN_ERROR` | `ErrorContent` | ✅ Preserved |
| `TEXT_MESSAGE_START` | (internal tracking) | ✅ Preserved |
| `TEXT_MESSAGE_CONTENT` | `TextContent` | ✅ Preserved |
| `TEXT_MESSAGE_END` | (internal tracking) | ✅ Preserved |
| `TOOL_CALL_START/ARGS/END` | `FunctionCallContent` | ✅ Preserved |
| `TOOL_CALL_RESULT` | `FunctionResultContent` | ✅ Preserved |
| `STATE_SNAPSHOT` | `DataContent` (application/json) | ✅ Preserved |
| `STATE_DELTA` | `DataContent` (application/json-patch+json) | ✅ Preserved |
| `MESSAGES_SNAPSHOT` | Custom handling | ✅ Preserved |
| `STEP_STARTED` | No direct mapping | ✅ Preserved |
| `STEP_FINISHED` | No direct mapping | ✅ Preserved |
| `ACTIVITY_*` | No direct mapping | ✅ Preserved |
| `THINKING_*` | `TextReasoningContent` (if available) | ✅ Preserved |
| `CUSTOM` | No direct mapping | ✅ Preserved |
| `RAW` | No direct mapping | ✅ Preserved |

### Client-Side Event Access

Users can access the original AG-UI event directly via `RawRepresentation`:

```csharp
// Usage - direct access via RawRepresentation
await foreach (var update in client.GetStreamingResponseAsync(messages))
{
    if (update.RawRepresentation is StepStartedEvent stepStarted)
    {
        Console.WriteLine($"Step started: {stepStarted.StepName}");
    }
    else if (update.RawRepresentation is StepFinishedEvent stepFinished)
    {
        Console.WriteLine($"Step completed: {stepFinished.StepId}");
    }
    else if (update.Text is { } text)
    {
        Console.Write(text);
    }
}
```

---

## 5. Missing AG-UI Events

### Events to Implement

| Event | Description | Priority |
|-------|-------------|----------|
| `STEP_STARTED` | Start of a step within agent run | High |
| `STEP_FINISHED` | Completion of a step | High |
| `MESSAGES_SNAPSHOT` | Snapshot of all conversation messages | High |
| `ACTIVITY_SNAPSHOT` | Complete snapshot of an activity | Medium |
| `ACTIVITY_DELTA` | Incremental activity updates | Medium |
| `THINKING_TEXT_MESSAGE_*` | Model reasoning/thinking events | Medium |
| `RAW` | Pass-through events from external systems | Low |
| `CUSTOM` | Application-specific events | Low |

### Tracking Issues
- [#2558](https://github.com/microsoft/agent-framework/issues/2558) - Support more AG-UI event types
- [#2510](https://github.com/microsoft/agent-framework/issues/2510) - MESSAGES_SNAPSHOT support
- [#2619](https://github.com/microsoft/agent-framework/issues/2619) - Reasoning/Thinking events

---

## 6. Workflow Support

### Goal

Enable workflows to work seamlessly with AG-UI, including:
- Proper event emission for workflow steps
- Client-side tool support in workflows
- Activity tracking for multi-agent scenarios

### Related Issues
- [#2494](https://github.com/microsoft/agent-framework/issues/2494) - AG-UI support for workflow as agent
- [#3002](https://github.com/microsoft/agent-framework/issues/3002) - Workflow doesn't recognize client-side tools

### Implementation

#### 6.1 Workflow-to-AG-UI Event Mapping

```csharp
// Map workflow events to AG-UI events
public static async IAsyncEnumerable<BaseEvent> AsAGUIEventsAsync(
    this IAsyncEnumerable<WorkflowEvent> workflowEvents,
    string threadId,
    string runId)
{
    yield return new RunStartedEvent { ThreadId = threadId, RunId = runId };
    
    await foreach (var evt in workflowEvents)
    {
        switch (evt)
        {
            case WorkflowStepStartedEvent step:
                yield return new StepStartedEvent
                {
                    StepId = step.StepId,
                    StepName = step.StepName,
                    ParentStepId = step.ParentStepId
                };
                break;
                
            case WorkflowStepFinishedEvent step:
                yield return new StepFinishedEvent { StepId = step.StepId };
                break;
                
            case WorkflowAgentResponseEvent response:
                // Convert agent response to AG-UI text/tool events
                foreach (var aguiEvent in response.ToAGUIEvents())
                {
                    yield return aguiEvent;
                }
                break;
                
            case WorkflowHandoffEvent handoff:
                yield return new ActivitySnapshotEvent
                {
                    ActivityId = $"handoff_{handoff.TargetAgent}",
                    Name = "Handoff",
                    Details = new { from = handoff.SourceAgent, to = handoff.TargetAgent }
                };
                break;
        }
    }
    
    yield return new RunFinishedEvent { ThreadId = threadId, RunId = runId };
}
```

#### 6.2 Client-Side Tools in Workflows

```csharp
// In AGUIEndpointRouteBuilderExtensions.MapAGUI
var runOptions = new ChatClientAgentRunOptions
{
    ChatOptions = new ChatOptions
    {
        // Pass client tools to the workflow
        Tools = clientTools,
        AdditionalProperties = new AdditionalPropertiesDictionary
        {
            ["ag_ui_client_tools"] = clientTools, // For workflows to access
            // ...
        }
    }
};

// In WorkflowAgent - propagate tools to participant agents
protected override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingCoreAsync(...)
{
    var clientTools = options?.ChatOptions?.AdditionalProperties?
        .GetValueOrDefault("ag_ui_client_tools") as IList<AITool>;
    
    // Include client tools when invoking participant agents
    foreach (var agent in participants)
    {
        var agentOptions = new ChatClientAgentRunOptions
        {
            ChatOptions = new ChatOptions { Tools = clientTools }
        };
        // ...
    }
}
```

---

## 7. Interruptions

### Goal

Support the AG-UI interruption protocol for canceling or pausing long-running agent operations.

### API Design

```csharp
/// <summary>
/// Represents an interrupt request from the client.
/// </summary>
public sealed class InterruptRequest
{
    public required string ThreadId { get; set; }
    public required string RunId { get; set; }
    public InterruptType Type { get; set; } = InterruptType.Cancel;
}

public enum InterruptType
{
    Cancel,
    Pause
}

/// <summary>
/// Event emitted when a run is interrupted.
/// </summary>
public sealed class RunInterruptedEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.RunInterrupted;
    public required string ThreadId { get; set; }
    public required string RunId { get; set; }
    public required InterruptType InterruptType { get; set; }
}
```

### Server-Side Implementation

```csharp
// In AGUIEndpointRouteBuilderExtensions
public static IEndpointConventionBuilder MapAGUIWithInterrupt(
    this IEndpointRouteBuilder endpoints,
    [StringSyntax("route")] string pattern,
    AIAgent aiAgent)
{
    var runCancellations = new ConcurrentDictionary<string, CancellationTokenSource>();
    
    // Main agent endpoint
    endpoints.MapPost(pattern, async (RunAgentInput input, HttpContext context, CancellationToken ct) =>
    {
        var runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        runCancellations[input.RunId] = runCts;
        
        try
        {
            // Run agent with cancellation support
            var events = aiAgent.RunStreamingAsync(messages, options: runOptions, cancellationToken: runCts.Token)
                .AsAGUIEventStreamAsync(input.ThreadId, input.RunId, jsonOptions, runCts.Token);
            
            return new AGUIServerSentEventsResult(events);
        }
        finally
        {
            runCancellations.TryRemove(input.RunId, out _);
        }
    });
    
    // Interrupt endpoint
    return endpoints.MapPost($"{pattern}/interrupt", async (InterruptRequest request) =>
    {
        if (runCancellations.TryGetValue(request.RunId, out var cts))
        {
            cts.Cancel();
            return Results.Ok(new { interrupted = true });
        }
        return Results.NotFound();
    });
}
```

### Client-Side Implementation

```csharp
public sealed class AGUIChatClient
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeRuns = new();
    
    public async Task InterruptAsync(string runId, CancellationToken cancellationToken = default)
    {
        var request = new InterruptRequest { RunId = runId };
        await _httpService.PostInterruptAsync(request, cancellationToken);
        
        // Also cancel locally
        if (_activeRuns.TryGetValue(runId, out var cts))
        {
            cts.Cancel();
        }
    }
}
```

---

## 8. Multi-Modal Inputs

### Goal

Support multi-modal content in AG-UI messages, including images, audio, and files.

### API Design

```csharp
/// <summary>
/// Represents multi-modal content in an AG-UI message.
/// </summary>
public abstract class AGUIContent
{
    public abstract string Type { get; }
}

public sealed class AGUITextContent : AGUIContent
{
    public override string Type => "text";
    public required string Text { get; set; }
}

public sealed class AGUIImageContent : AGUIContent
{
    public override string Type => "image";
    public string? Url { get; set; }
    public string? Base64Data { get; set; }
    public string? MimeType { get; set; }
}

public sealed class AGUIAudioContent : AGUIContent
{
    public override string Type => "audio";
    public string? Url { get; set; }
    public string? Base64Data { get; set; }
    public string? MimeType { get; set; }
}

public sealed class AGUIFileContent : AGUIContent
{
    public override string Type => "file";
    public required string Name { get; set; }
    public string? Url { get; set; }
    public string? Base64Data { get; set; }
    public string? MimeType { get; set; }
}

/// <summary>
/// Updated message with multi-modal content support.
/// </summary>
public sealed class AGUIUserMessage : AGUIMessage
{
    public override string Role => AGUIRoles.User;
    
    // Simple text content (backwards compatible)
    public string? Content { get; set; }
    
    // Multi-modal content
    public IList<AGUIContent>? Contents { get; set; }
}
```

### Conversion to M.E.AI

```csharp
public static ChatMessage AsChatMessage(this AGUIUserMessage message, JsonSerializerOptions options)
{
    var contents = new List<AIContent>();
    
    // Handle simple text content
    if (!string.IsNullOrEmpty(message.Content))
    {
        contents.Add(new TextContent(message.Content));
    }
    
    // Handle multi-modal contents
    if (message.Contents is { Count: > 0 })
    {
        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case AGUITextContent text:
                    contents.Add(new TextContent(text.Text));
                    break;
                    
                case AGUIImageContent image:
                    contents.Add(image.Url is not null
                        ? new ImageContent(new Uri(image.Url))
                        : new ImageContent(Convert.FromBase64String(image.Base64Data!), image.MimeType));
                    break;
                    
                case AGUIAudioContent audio:
                    contents.Add(new DataContent(
                        audio.Url is not null ? audio.Url : Convert.FromBase64String(audio.Base64Data!),
                        audio.MimeType ?? "audio/wav"));
                    break;
                    
                case AGUIFileContent file:
                    contents.Add(new DataContent(
                        file.Base64Data is not null ? Convert.FromBase64String(file.Base64Data) : file.Url!,
                        file.MimeType ?? "application/octet-stream"));
                    break;
            }
        }
    }
    
    return new ChatMessage(ChatRole.User, contents) { MessageId = message.Id };
}
```

---

## 9. Bug Fixes & Improvements

### Critical Bugs (P0)

| Issue | Description | PR Status |
|-------|-------------|-----------|
| [#3475](https://github.com/microsoft/agent-framework/issues/3475) | Thread ID inconsistency (`ag_ui_thread_id` vs `agui_thread_id`) | ❌ No PR |
| [#3433](https://github.com/microsoft/agent-framework/issues/3433) | MessageId null causing CopilotKit validation failures | ❌ No PR |
| [#2775](https://github.com/microsoft/agent-framework/issues/2775) | JSON parsing exception for plain string tool results | ✅ PR #2871 (Draft) by @Copilot |
| [#2637](https://github.com/microsoft/agent-framework/issues/2637) | parentMessageId serialization breaking TypeScript clients | ❌ No PR |
| [#3365](https://github.com/microsoft/agent-framework/issues/3365) | AGUIToolMessage MessageId not set | ❌ No PR |

### High Priority Improvements (P1)

| Issue | Description | PR Status |
|-------|-------------|-----------|
| [#2988](https://github.com/microsoft/agent-framework/issues/2988) | Dynamic agent resolution in MapAGUI | ✅ PR #2547 by @javiercn |
| [#2179](https://github.com/microsoft/agent-framework/issues/2179) | MapAGUI agent name support | ✅ PR #2547 by @javiercn |
| [#2517](https://github.com/microsoft/agent-framework/issues/2517) | Thread persistence support | ❌ No PR |
| [#3216](https://github.com/microsoft/agent-framework/issues/3216) | Client-side event type detection | ❌ No PR |
| [#2699](https://github.com/microsoft/agent-framework/issues/2699) | Multi-turn tool calls history ordering | ❌ No PR |
| [#2081](https://github.com/microsoft/agent-framework/issues/2081) | State management from tools | ❌ No PR |

### Integration Issues (P2)

| Issue | Description | PR Status |
|-------|-------------|-----------|
| [#3215](https://github.com/microsoft/agent-framework/issues/3215) | Copilot Studio AG-UI integration (.NET) | ❌ No PR |
| [#3203](https://github.com/microsoft/agent-framework/issues/3203) | Copilot Studio AG-UI integration (Python) | ❌ No PR |
| [#2959](https://github.com/microsoft/agent-framework/issues/2959) | Azure AI Projects ignores per-request tools | ❌ No PR |
| [#2702](https://github.com/microsoft/agent-framework/issues/2702) | OpenAI Responses API thread ID handling | ❌ No PR |

---

## 10. Active Pull Requests

### Team PRs (Tracked)

| PR | Title | Author | Addresses | Status |
|----|-------|--------|-----------|--------|
| [#2871](https://github.com/microsoft/agent-framework/pull/2871) | Fix JSON parsing exception when tool returns plain string | @Copilot | #2775 | 📝 Draft |
| [#2547](https://github.com/microsoft/agent-framework/pull/2547) | Add MapAGUI hosting overloads with IHostedAgentBuilder | @javiercn | #2179, #2988 | ✅ Ready |

### Community PRs (Not Tracked)

The following community PRs address AG-UI issues but require evaluation:
- PR #3162 by @TheEagleByte - Dynamic agent resolution with factory delegate
- PR #2700 by @emilmuller - Multi-turn tool calls fix
- PR #2343 by @halllo - Per-request agent selection
- PR #3367 by @MaciejWarchalowski - AGUIToolMessage MessageId fix

---

## References

- [AG-UI Protocol Documentation](https://docs.ag-ui.com/introduction)
- [AG-UI Events Specification](https://docs.ag-ui.com/concepts/events)
- [AG-UI GitHub](https://github.com/ag-ui-protocol)
- [Microsoft Agent Framework Samples](../samples/)
