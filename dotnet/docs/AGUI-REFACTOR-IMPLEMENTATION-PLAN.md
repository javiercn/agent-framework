# AG-UI Protocol Package Refactor - Implementation Plan

## Executive Summary

This document provides a detailed implementation plan for creating the `AGUI.Protocol` package and making AG-UI event types public. The plan focuses strictly on:

1. Creating a new standalone protocol package with public types
2. Implementing missing AG-UI event types per the protocol specification
3. Enabling users to emit and inspect AG-UI events directly
4. Fixing critical bugs in existing AG-UI serialization/deserialization

**Out of Scope:** Workflow integration, multimodal support, Azure AI Projects enhancements, server-side hosting improvements.

---

## Phase 1: Create `AGUI.Protocol` Package

### 1.1 Package Setup

**Goal:** Create a new standalone package with all AG-UI protocol types as public APIs.

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P1.1.1 | Create new project `src/AGUI.Protocol/AGUI.Protocol.csproj` | High |
| P1.1.2 | Define root namespace as `AGUI.Protocol` | High |
| P1.1.3 | Configure package metadata (NuGet, README, license) | High |
| P1.1.4 | Add package to solution file | High |

### 1.2 Move and Publicize Event Types

**Goal:** Move all event types from internal `Shared/` folders to public namespace.

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P1.2.1 | Make `BaseEvent` class `public abstract` | High |
| P1.2.2 | Make `BaseEventJsonConverter` class `public` | High |
| P1.2.3 | Make `AGUIEventTypes` constants class `public static` | High |
| P1.2.4 | Make all existing event classes `public sealed` | High |
| P1.2.5 | Create public `AGUIJsonSerializerContext` | High |

### 1.3 Move and Publicize Message Types

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P1.3.1 | Make `AGUIMessage` class `public abstract` | High |
| P1.3.2 | Make `AGUIUserMessage`, `AGUIAssistantMessage`, `AGUIToolMessage`, `AGUISystemMessage`, `AGUIDeveloperMessage` `public sealed` | High |
| P1.3.3 | Make `AGUIMessageJsonConverter` class `public` | High |
| P1.3.4 | Make `AGUIRoles` constants class `public static` | High |

### 1.4 Move and Publicize Tool/Context Types

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P1.4.1 | Make `AGUITool`, `AGUIToolCall`, `AGUIFunctionCall` classes `public` | High |
| P1.4.2 | Make `AGUIContextItem` class `public` | High |
| P1.4.3 | Make `RunAgentInput` class `public` | High |

### 1.5 Package Structure

```
src/AGUI.Protocol/
├── AGUI.Protocol.csproj
├── Events/
│   ├── BaseEvent.cs
│   ├── BaseEventJsonConverter.cs
│   ├── AGUIEventTypes.cs
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
│   ├── MessagesSnapshotEvent.cs        (NEW)
│   ├── StepStartedEvent.cs             (NEW)
│   ├── StepFinishedEvent.cs            (NEW)
│   ├── ActivitySnapshotEvent.cs        (NEW)
│   ├── ActivityDeltaEvent.cs           (NEW)
│   ├── ThinkingStartEvent.cs           (NEW)
│   ├── ThinkingEndEvent.cs             (NEW)
│   ├── ThinkingTextMessageStartEvent.cs    (NEW)
│   ├── ThinkingTextMessageContentEvent.cs  (NEW)
│   ├── ThinkingTextMessageEndEvent.cs      (NEW)
│   ├── RawEvent.cs                     (NEW)
│   └── CustomEvent.cs                  (NEW)
├── Messages/
│   ├── AGUIMessage.cs
│   ├── AGUIMessageJsonConverter.cs
│   ├── AGUIRoles.cs
│   ├── AGUIUserMessage.cs
│   ├── AGUIAssistantMessage.cs
│   ├── AGUIToolMessage.cs
│   ├── AGUISystemMessage.cs
│   └── AGUIDeveloperMessage.cs
├── Tools/
│   ├── AGUITool.cs
│   ├── AGUIToolCall.cs
│   └── AGUIFunctionCall.cs
├── Context/
│   ├── AGUIContextItem.cs
│   └── RunAgentInput.cs
└── Serialization/
    ├── AGUIJsonSerializerContext.cs
    └── AGUIJsonSerializerOptions.cs
```

### 1.6 Update Dependent Packages

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P1.6.1 | Add `AGUI.Protocol` package reference to `Microsoft.Agents.AI.AGUI` | High |
| P1.6.2 | Add `AGUI.Protocol` package reference to `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | High |
| P1.6.3 | Remove `Shared/` folder from `Microsoft.Agents.AI.AGUI` | High |
| P1.6.4 | Remove `Shared/` folder from `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | High |
| P1.6.5 | Remove file linking between packages | High |
| P1.6.6 | Update namespace imports in dependent code | High |
| P1.6.7 | Update unit tests to use public types from `AGUI.Protocol` | Medium |

---

## Phase 2: Implement Missing Event Types

### 2.1 Step Events (STEP_STARTED, STEP_FINISHED)

**AG-UI Spec Reference:** [Steps Events](https://docs.ag-ui.com/concepts/events#step-events)

#### Event Definitions

```csharp
public sealed class StepStartedEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.StepStarted;

    [JsonPropertyName("stepId")]
    public required string StepId { get; set; }

    [JsonPropertyName("stepName")]
    public required string StepName { get; set; }

    [JsonPropertyName("parentStepId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentStepId { get; set; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Metadata { get; set; }
}

public sealed class StepFinishedEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.StepFinished;

    [JsonPropertyName("stepId")]
    public required string StepId { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; } // "completed", "failed", "skipped"

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; set; }
}
```

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P2.1.1 | Add `StepStarted` and `StepFinished` constants to `AGUIEventTypes` | High |
| P2.1.2 | Implement `StepStartedEvent` class | High |
| P2.1.3 | Implement `StepFinishedEvent` class | High |
| P2.1.4 | Register types in `AGUIJsonSerializerContext` | High |
| P2.1.5 | Add unit tests | High |

### 2.2 Activity Events (ACTIVITY_SNAPSHOT, ACTIVITY_DELTA)

**AG-UI Spec Reference:** [Activity Events](https://docs.ag-ui.com/concepts/events#activity-events)

#### Event Definitions

```csharp
public sealed class ActivitySnapshotEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.ActivitySnapshot;

    [JsonPropertyName("activityId")]
    public required string ActivityId { get; set; }

    [JsonPropertyName("activityType")]
    public required string ActivityType { get; set; }

    [JsonPropertyName("state")]
    public required JsonElement State { get; set; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Metadata { get; set; }
}

public sealed class ActivityDeltaEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.ActivityDelta;

    [JsonPropertyName("activityId")]
    public required string ActivityId { get; set; }

    [JsonPropertyName("delta")]
    public required JsonElement Delta { get; set; }
}
```

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P2.2.1 | Add `ActivitySnapshot` and `ActivityDelta` constants to `AGUIEventTypes` | High |
| P2.2.2 | Implement `ActivitySnapshotEvent` class | High |
| P2.2.3 | Implement `ActivityDeltaEvent` class | High |
| P2.2.4 | Register types in `AGUIJsonSerializerContext` | High |
| P2.2.5 | Add unit tests | High |

### 2.3 Thinking Events (THINKING_*)

**AG-UI Spec Reference:** [Thinking Events](https://docs.ag-ui.com/concepts/events#thinking-events)

**Related Issue:** [#2619](https://github.com/microsoft/agent-framework/issues/2619)

#### Event Definitions

```csharp
public sealed class ThinkingStartEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.ThinkingStart;
}

public sealed class ThinkingEndEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.ThinkingEnd;
}

public sealed class ThinkingTextMessageStartEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.ThinkingTextMessageStart;

    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }
}

public sealed class ThinkingTextMessageContentEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.ThinkingTextMessageContent;

    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }

    [JsonPropertyName("delta")]
    public required string Delta { get; set; }
}

public sealed class ThinkingTextMessageEndEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.ThinkingTextMessageEnd;

    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }
}
```

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P2.3.1 | Add all `Thinking*` constants to `AGUIEventTypes` | High |
| P2.3.2 | Implement `ThinkingStartEvent` class | High |
| P2.3.3 | Implement `ThinkingEndEvent` class | High |
| P2.3.4 | Implement `ThinkingTextMessageStartEvent` class | High |
| P2.3.5 | Implement `ThinkingTextMessageContentEvent` class | High |
| P2.3.6 | Implement `ThinkingTextMessageEndEvent` class | High |
| P2.3.7 | Register types in `AGUIJsonSerializerContext` | High |
| P2.3.8 | Add unit tests | High |

### 2.4 Messages Snapshot Event (MESSAGES_SNAPSHOT)

**AG-UI Spec Reference:** [Messages Snapshot](https://docs.ag-ui.com/concepts/events#messages-snapshot)

**Related Issue:** [#2510](https://github.com/microsoft/agent-framework/issues/2510)

#### Event Definition

```csharp
public sealed class MessagesSnapshotEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.MessagesSnapshot;

    [JsonPropertyName("messages")]
    public required IReadOnlyList<AGUIMessage> Messages { get; set; }
}
```

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P2.4.1 | Add `MessagesSnapshot` constant to `AGUIEventTypes` | High |
| P2.4.2 | Implement `MessagesSnapshotEvent` class | High |
| P2.4.3 | Register type in `AGUIJsonSerializerContext` | High |
| P2.4.4 | Add unit tests | High |

### 2.5 Raw and Custom Events

**AG-UI Spec Reference:** [Custom Events](https://docs.ag-ui.com/concepts/events#custom-events)

#### Event Definitions

```csharp
public sealed class RawEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.Raw;

    [JsonPropertyName("source")]
    public required string Source { get; set; }

    [JsonPropertyName("event")]
    public required JsonElement Event { get; set; }
}

public sealed class CustomEvent : BaseEvent
{
    public override string Type => AGUIEventTypes.Custom;

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("value")]
    public required JsonElement Value { get; set; }
}
```

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P2.5.1 | Add `Raw` and `Custom` constants to `AGUIEventTypes` | Medium |
| P2.5.2 | Implement `RawEvent` class | Medium |
| P2.5.3 | Implement `CustomEvent` class | Medium |
| P2.5.4 | Register types in `AGUIJsonSerializerContext` | Medium |
| P2.5.5 | Add unit tests | Medium |

---

## Phase 3: Event Emission and Inspection API

### 3.1 AGUIEventContent Helper Class

**Goal:** Enable users to create and extract AG-UI events from `DataContent`.

#### Implementation

```csharp
namespace AGUI.Protocol;

/// <summary>
/// Helper class for creating and extracting AG-UI events as DataContent.
/// </summary>
public static class AGUIEventContent
{
    private const string MediaTypePrefix = "application/ag-ui.";
    private const string MediaTypeSuffix = "+json";

    /// <summary>
    /// Creates a DataContent representing an AG-UI event.
    /// </summary>
    public static DataContent Create<TEvent>(TEvent evt, JsonSerializerOptions? options = null)
        where TEvent : BaseEvent
    {
        options ??= AGUIJsonSerializerContext.Default.Options;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(evt, options);
        var mediaType = $"{MediaTypePrefix}{evt.Type.ToLowerInvariant()}{MediaTypeSuffix}";
        return new DataContent(bytes, mediaType);
    }

    /// <summary>
    /// Tries to extract an AG-UI event from a DataContent.
    /// </summary>
    public static bool TryGetEvent(DataContent content, out BaseEvent? evt, JsonSerializerOptions? options = null)
    {
        evt = null;
        if (content.MediaType is null || !content.MediaType.StartsWith(MediaTypePrefix))
            return false;

        options ??= AGUIJsonSerializerContext.Default.Options;
        evt = JsonSerializer.Deserialize<BaseEvent>(content.Data.Span, options);
        return evt != null;
    }

    /// <summary>
    /// Checks if a DataContent represents an AG-UI event.
    /// </summary>
    public static bool IsAGUIEvent(DataContent content)
    {
        return content.MediaType?.StartsWith(MediaTypePrefix) == true;
    }
}
```

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P3.1.1 | Implement `AGUIEventContent.Create<TEvent>()` method | High |
| P3.1.2 | Implement `AGUIEventContent.TryGetEvent()` method | High |
| P3.1.3 | Implement `AGUIEventContent.IsAGUIEvent()` method | High |
| P3.1.4 | Add unit tests for all methods | High |

### 3.2 Update Event Conversion Extensions

**Goal:** Handle AG-UI events embedded in DataContent during stream conversion.

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P3.2.1 | Update `AsAGUIEventStreamAsync()` to detect AG-UI events in DataContent | High |
| P3.2.2 | Emit detected AG-UI events directly to the stream | High |
| P3.2.3 | Add unit tests for DataContent AG-UI event handling | High |

### 3.3 RawRepresentation Preservation

**Goal:** Preserve original AG-UI events on `ChatResponseUpdate.RawRepresentation`.

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P3.3.1 | Update `AsChatResponseUpdatesAsync()` to set `RawRepresentation` for all events | High |
| P3.3.2 | Add unit tests verifying `RawRepresentation` is set | High |

---

## Phase 4: Bug Fixes

### 4.1 Thread ID Inconsistency

**Issue:** [#3475](https://github.com/microsoft/agent-framework/issues/3475)

**Problem:** Inconsistent usage of `ag_ui_thread_id` vs `agui_thread_id`.

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P4.1.1 | Add `AGUIPropertyNames` constants class to `AGUI.Protocol` | High |
| P4.1.2 | Define `ThreadId = "ag_ui_thread_id"` constant | High |
| P4.1.3 | Update all usages to use the constant | High |
| P4.1.4 | Add unit tests for consistency | High |

### 4.2 MessageId Null for Tool Messages

**Issue:** [#3365](https://github.com/microsoft/agent-framework/issues/3365)

**Problem:** `AGUIToolMessage.Id` not mapped to `ChatMessage.MessageId`.

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P4.2.1 | Update `AGUIChatMessageExtensions.AsChatMessages()` to set MessageId from AGUIToolMessage.Id | High |
| P4.2.2 | Add unit test | High |

### 4.3 JSON Parsing Exception for Plain String Content

**Issue:** [#2775](https://github.com/microsoft/agent-framework/issues/2775)

**Problem:** `DeserializeResultIfAvailable` throws when tool returns plain string.

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P4.3.1 | Update `DeserializeResultIfAvailable` to try-catch JSON parsing | High |
| P4.3.2 | Return original string if JSON parsing fails | High |
| P4.3.3 | Add unit tests for string, JSON object, and JSON array results | High |

### 4.4 parentMessageId Null Serialization

**Issue:** [#2637](https://github.com/microsoft/agent-framework/issues/2637)

**Problem:** `parentMessageId: null` breaks @ag-ui/core validation.

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P4.4.1 | Add `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` to all nullable properties | High |
| P4.4.2 | Audit all event classes for nullable properties | High |
| P4.4.3 | Add unit tests verifying null properties are omitted | High |

### 4.5 Multi-turn Tool Calls History Ordering

**Issue:** [#2699](https://github.com/microsoft/agent-framework/issues/2699)

**Problem:** Multiple tool calls produce invalid message ordering for OpenAI.

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P4.5.1 | Review `AsChatMessages()` message ordering logic | High |
| P4.5.2 | Ensure assistant tool_calls are immediately followed by tool results | High |
| P4.5.3 | Add unit tests for multi-tool-call scenarios | High |

---

## Phase 5: Documentation and Tests

### 5.1 Package Documentation

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P5.1.1 | Create README.md for `AGUI.Protocol` package | High |
| P5.1.2 | Add XML documentation to all public types | High |
| P5.1.3 | Add code examples for event creation and inspection | High |

### 5.2 Unit Tests

#### Tasks

| Task ID | Description | Priority |
|---------|-------------|----------|
| P5.2.1 | Create `AGUI.Protocol.UnitTests` test project | High |
| P5.2.2 | Add serialization/deserialization tests for all event types | High |
| P5.2.3 | Add round-trip tests for `AGUIEventContent` | High |
| P5.2.4 | Add tests for `BaseEventJsonConverter` polymorphic deserialization | High |

---

## Complete Event Types Reference

### Currently Implemented (Internal)

| Event Type | Constant | Status |
|------------|----------|--------|
| RUN_STARTED | `RunStarted` | ✅ Move to public |
| RUN_FINISHED | `RunFinished` | ✅ Move to public |
| RUN_ERROR | `RunError` | ✅ Move to public |
| TEXT_MESSAGE_START | `TextMessageStart` | ✅ Move to public |
| TEXT_MESSAGE_CONTENT | `TextMessageContent` | ✅ Move to public |
| TEXT_MESSAGE_END | `TextMessageEnd` | ✅ Move to public |
| TOOL_CALL_START | `ToolCallStart` | ✅ Move to public |
| TOOL_CALL_ARGS | `ToolCallArgs` | ✅ Move to public |
| TOOL_CALL_END | `ToolCallEnd` | ✅ Move to public |
| TOOL_CALL_RESULT | `ToolCallResult` | ✅ Move to public |
| STATE_SNAPSHOT | `StateSnapshot` | ✅ Move to public |
| STATE_DELTA | `StateDelta` | ✅ Move to public |

### To Be Implemented (New)

| Event Type | Constant | Priority |
|------------|----------|----------|
| STEP_STARTED | `StepStarted` | High |
| STEP_FINISHED | `StepFinished` | High |
| ACTIVITY_SNAPSHOT | `ActivitySnapshot` | High |
| ACTIVITY_DELTA | `ActivityDelta` | High |
| MESSAGES_SNAPSHOT | `MessagesSnapshot` | High |
| THINKING_START | `ThinkingStart` | High |
| THINKING_END | `ThinkingEnd` | High |
| THINKING_TEXT_MESSAGE_START | `ThinkingTextMessageStart` | High |
| THINKING_TEXT_MESSAGE_CONTENT | `ThinkingTextMessageContent` | High |
| THINKING_TEXT_MESSAGE_END | `ThinkingTextMessageEnd` | High |
| RAW | `Raw` | Medium |
| CUSTOM | `Custom` | Medium |

---

## GitHub Issues Addressed

### Critical Bugs

| Issue | Title |
|-------|-------|
| [#3475](https://github.com/microsoft/agent-framework/issues/3475) | Thread ID inconsistency (`ag_ui_thread_id` vs `agui_thread_id`) |
| [#3365](https://github.com/microsoft/agent-framework/issues/3365) | MessageId null for tool messages |
| [#2775](https://github.com/microsoft/agent-framework/issues/2775) | JSON parsing exception for plain string content |
| [#2637](https://github.com/microsoft/agent-framework/issues/2637) | parentMessageId null serialization |
| [#2699](https://github.com/microsoft/agent-framework/issues/2699) | Multi-turn tool calls history ordering |

### Feature Requests

| Issue | Title |
|-------|-------|
| [#2558](https://github.com/microsoft/agent-framework/issues/2558) | Support more AG-UI event types |
| [#2619](https://github.com/microsoft/agent-framework/issues/2619) | Support THINKING_TEXT_MESSAGE events |
| [#2510](https://github.com/microsoft/agent-framework/issues/2510) | Sync conversation history (MESSAGES_SNAPSHOT) |

---

## Phase Dependencies

| Phase | Description | Dependencies |
|-------|-------------|--------------|
| Phase 1 | Create `AGUI.Protocol` Package | None |
| Phase 2 | Implement Missing Event Types | Phase 1 |
| Phase 3 | Event Emission and Inspection API | Phase 1, 2 |
| Phase 4 | Bug Fixes | Phase 1 |
| Phase 5 | Documentation and Tests | Phase 1-4 |

---

## Success Criteria

1. ✅ New `AGUI.Protocol` NuGet package is published
2. ✅ All AG-UI event types are publicly accessible
3. ✅ All 12 missing event types are implemented
4. ✅ Users can create AG-UI events via `AGUIEventContent.Create<T>()`
5. ✅ Users can inspect raw AG-UI events via `ChatResponseUpdate.RawRepresentation`
6. ✅ All 5 critical bugs are fixed with regression tests
7. ✅ 100% XML documentation coverage on public types
8. ✅ No breaking changes to existing `Microsoft.Agents.AI.AGUI` public API
