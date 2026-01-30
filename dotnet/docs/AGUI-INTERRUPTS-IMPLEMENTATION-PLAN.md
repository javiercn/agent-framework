# AG-UI Interrupts Implementation Plan

## Overview

This document outlines the implementation plan for adding **Interrupt-Aware Run Lifecycle** support to the .NET AG-UI implementation. The interrupts feature provides native support for human-in-the-loop pauses and enables compatibility with various framework interrupts, workflow suspend/resume patterns, and other pause mechanisms.

## AG-UI Interrupts Protocol Summary

The AG-UI interrupts specification introduces a standardized interrupt/resume pattern:

1. **Agent sends interrupt**: When a run needs human input, the agent emits a `RUN_FINISHED` event with `outcome: "interrupt"` and an `interrupt` payload
2. **User responds**: The client sends a new `RunAgentInput` with a `resume` payload containing the response
3. **Agent continues**: The agent processes the resume and completes (or interrupts again)

### Key Protocol Changes

#### RunFinishedEvent Updates
```typescript
type RunFinishedOutcome = "success" | "interrupt"

type RunFinished = {
  type: "RUN_FINISHED"
  outcome?: RunFinishedOutcome  // optional for back-compat
  result?: any                   // Present when outcome === "success"
  interrupt?: {
    id?: string                  // Unique interrupt identifier
    payload?: any                // JSON with function details (for approval) or custom data (for user input)
  }
}
```

#### RunAgentInput Updates
```typescript
type RunAgentInput = {
  // ... existing fields
  resume?: {
    interruptId?: string         // Echo back the interrupt id
    payload?: any                // Response data from user
  }
}
```

## Implementation Approach

### Mapping Strategy

The interrupts feature maps directly to existing Microsoft.Extensions.AI (MEAI) content types. We support **two interrupt types**:

| MEAI Request Content Type | MEAI Response Content Type | Description |
|---------------------------|----------------------------|-------------|
| `FunctionApprovalRequestContent` | `FunctionApprovalResponseContent` | Built-in MEAI type for function/tool approval workflows |
| `UserInputRequestContent` | `UserInputResponseContent` | Built-in MEAI type for arbitrary user input requests |

**Key design decision:** Instead of creating custom content types, we use the `RawRepresentation` property (available on all `AIContent` types) to store the original `AGUIInterrupt` payload. This follows the idiomatic MEAI pattern used throughout the codebase for provider-specific data.

### Interrupt Payload Structure and ID Mapping

#### Function Approval Interrupts

When mapping `FunctionApprovalRequestContent` to an AG-UI interrupt:

- **Interrupt ID** ← `FunctionApprovalRequestContent.Id` (the approval ID)
- **Payload** includes the function details from `FunctionApprovalRequestContent.FunctionCall`:
  - `functionName`: The name of the function/tool being called
  - `functionArguments`: The arguments passed to the function

Example interrupt for function approval:
```json
{
  "id": "call-abc",
  "payload": {
    "functionName": "get_weather",
    "functionArguments": {
      "location": "Seattle"
    }
  }
}
```

#### User Input Interrupts

When mapping `UserInputRequestContent` to an AG-UI interrupt:

- **Interrupt ID** ← `UserInputRequestContent.Id`
- **Payload** is stored in `RawRepresentation` as the original `AGUIInterrupt` or `JsonElement`

Example interrupt for user input:
```json
{
  "id": "input-456",
  "payload": {
    "prompt": "Please provide your email address",
    "inputType": "email"
  }
}
```

#### Reverse Mapping (AG-UI → MEAI)

When receiving an AG-UI interrupt:
1. Check if payload contains `functionName` → create `FunctionApprovalRequestContent`
2. Otherwise → create `UserInputRequestContent` with `RawRepresentation = aguiInterrupt`

### Comparison with Existing Tool-Based Approach (Step04_HumanInLoop)

The existing `Step04_HumanInLoop` sample uses **tool calling** for human-in-the-loop:
- Uses a synthetic `request_approval` tool call
- Client intercepts the tool call and prompts user
- User response is sent back as a tool result
- Requires middleware transformation on both server and client

The new **interrupts-based approach** provides:
- Native protocol support (no synthetic tools)
- Cleaner separation of concerns
- Protocol-level pause/resume semantics
- Better compatibility with workflow systems

## Implementation Tasks

### Phase 1: Protocol Layer (AGUI.Protocol)

#### 1.1 Update RunFinishedEvent
**File**: `src/AGUI.Protocol/Events/RunFinishedEvent.cs`

Add new properties:
- `Outcome` (string): "success" | "interrupt"
- `Interrupt` (AGUIInterrupt): Interrupt payload object

Create new types:
- **File**: `src/AGUI.Protocol/Events/AGUIInterrupt.cs`
  - `Id` (string?): Optional interrupt identifier
  - `Payload` (JsonElement?): Arbitrary JSON payload for the interrupt (contains function details for approval requests, or custom data for user input requests)

Update event type constants:
- Add `RunFinishedOutcome` constants class

#### 1.2 Update RunAgentInput
**File**: `src/AGUI.Protocol/Context/RunAgentInput.cs`

Add new properties:
- `Resume` (AGUIResume?): Resume payload for continuing from interrupt

Create new types:
- **File**: `src/AGUI.Protocol/Context/AGUIResume.cs`
  - `InterruptId` (string?): Echo back the interrupt id
  - `Payload` (JsonElement?): Response data from user

#### 1.3 Update Serialization Context
**File**: `src/AGUI.Protocol/Serialization/AGUIJsonSerializerContext.cs`

Register new types for AOT serialization:
- `AGUIInterrupt`
- `AGUIResume`

### Phase 2: AGUI Client/Handler Layer (Microsoft.Agents.AI.AGUI)

#### 2.1 Update Event-to-MEAI Mappings
**File**: `src/Microsoft.Agents.AI.AGUI/Shared/ChatResponseUpdateAGUIExtensions.cs`

In `AsChatResponseUpdatesAsync`:
- Handle `RunFinishedEvent` with `Outcome == "interrupt"`
- Detect function approval by checking if payload contains `functionName` property
- For function approval: Create `FunctionApprovalRequestContent` with ID from interrupt ID
- For user input: Create `UserInputRequestContent` with `RawRepresentation = aguiInterrupt`
- Emit appropriate content type with interrupt metadata

In `AsAGUIEventStreamAsync`:
- Detect `FunctionApprovalRequestContent` and emit `RunFinishedEvent` with:
  - `interrupt.id` ← `FunctionApprovalRequestContent.Id`
  - `interrupt.payload` containing function name and arguments
- Detect `UserInputRequestContent` (non-approval) and emit `RunFinishedEvent` with interrupt
- Handle `FunctionApprovalResponseContent` and `UserInputResponseContent` to construct resume payload

#### 2.2 Update AGUIChatClient (if needed)
**File**: `src/Microsoft.Agents.AI.AGUI/AGUIChatClient.cs`

Ensure the client properly:
- Handles interrupted responses (doesn't treat as error)
- Passes resume data on subsequent requests
- Preserves thread ID across interrupt/resume cycles

#### 2.3 Create Interrupt Content Extensions
**File**: `src/Microsoft.Agents.AI.AGUI/Shared/InterruptContentExtensions.cs` (new)

Helper methods:
- `ToAGUIInterrupt(FunctionApprovalRequestContent)` → `AGUIInterrupt`
  - Sets `Id` from `FunctionApprovalRequestContent.Id`
  - Sets `Payload` with `functionName` and `functionArguments` from `FunctionCall`
- `ToAGUIInterrupt(UserInputRequestContent)` → `AGUIInterrupt`
  - Sets `Id` from `UserInputRequestContent.Id`
  - Sets `Payload` from `RawRepresentation` if available
- `FromAGUIInterrupt(AGUIInterrupt)` → `AIContent`
  - If payload has `functionName`: Create `FunctionApprovalRequestContent` with ID from interrupt ID
  - Otherwise: Create `UserInputRequestContent` with `RawRepresentation = aguiInterrupt`
- `ToAGUIResume(FunctionApprovalResponseContent)` → `AGUIResume`
- `ToAGUIResume(UserInputResponseContent)` → `AGUIResume`

### Phase 3: ASP.NET Core Hosting Layer

#### 3.1 Update Server-Side Request Handling
**File**: `src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIEndpointRouteBuilderExtensions.cs`

Handle incoming `RunAgentInput` with `Resume`:
- Extract resume payload
- Convert to appropriate MEAI response content
- Add to message history before invoking agent

Handle outgoing interrupt responses:
- Detect when agent yields interrupt content
- Emit `RunFinishedEvent` with `outcome: "interrupt"`

### Phase 4: Unit Tests

#### 4.1 Protocol Serialization Tests
**File**: `tests/AGUI.Protocol.UnitTests/InterruptSerializationTests.cs` (new)

Tests:
- `RunFinishedEvent_WithInterrupt_SerializesCorrectly`
- `RunFinishedEvent_WithSuccess_SerializesCorrectly`
- `RunFinishedEvent_WithoutOutcome_BackwardCompatible`
- `AGUIInterrupt_AllFields_SerializesCorrectly`
- `AGUIInterrupt_OnlyId_SerializesCorrectly`
- `AGUIResume_WithPayload_SerializesCorrectly`
- `RunAgentInput_WithResume_SerializesCorrectly`
- Roundtrip deserialization tests

#### 4.2 Event Mapping Tests
**File**: `tests/Microsoft.Agents.AI.AGUI.UnitTests/InterruptMappingTests.cs` (new)

Tests:
- `RunFinishedEvent_WithFunctionApprovalPayload_MapsToFunctionApprovalRequestContent`
- `RunFinishedEvent_WithUserInputPayload_MapsToUserInputRequestContent_WithRawRepresentation`
- `FunctionApprovalRequestContent_MapsToInterruptEvent_WithIdFromApprovalId`
- `FunctionApprovalRequestContent_MapsToInterruptEvent_WithFunctionDetailsInPayload`
- `UserInputRequestContent_MapsToInterruptEvent_WithRawRepresentationAsPayload`
- `FunctionApprovalResponseContent_MapsToResumePayload`
- `UserInputResponseContent_MapsToResumePayload`
- End-to-end interrupt/resume cycle tests

### Phase 5: Integration Tests

#### 5.1 Interrupt Integration Tests
**File**: `tests/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests/InterruptTests.cs` (new)

Tests:
- `Agent_WithFunctionApprovalRequest_EmitsInterruptEvent_WithApprovalIdAsInterruptId`
- `Agent_WithFunctionApprovalRequest_EmitsInterruptEvent_WithFunctionDetailsInPayload`
- `Agent_WithUserInputRequest_EmitsInterruptEvent_WithRawRepresentation`
- `Client_WithResumePayload_ContinuesExecution`
- `MultipleInterrupts_HandleCorrectly`
- `InterruptId_PreservedAcrossRoundTrip_ForFunctionApproval`
- `InterruptId_PreservedAcrossRoundTrip_ForUserInput`

### Phase 6: Samples

#### 6.1 Sample 1: Interrupts-Based Function Approval
**Location**: `samples/GettingStarted/AGUI/Step09_InterruptsApproval/`

Similar to Step04_HumanInLoop but uses native interrupts:

**Server/Program.cs**:
```csharp
// Configure agent with function tools that require approval
var agent = new ChatCompletionAgent(chatClient)
{
    Name = "WeatherAgent",
    Instructions = "You are a helpful weather assistant.",
    FunctionCallPolicy = FunctionCallPolicy.RequireApproval // Uses FunctionApprovalRequestContent
};

// Host as AGUI endpoint (interrupts are handled automatically)
app.MapAGUIAgent("/api/agent", agent);
```

**Server/InterruptApprovalAgent.cs**:
- Wrapper agent that uses `FunctionApprovalRequestContent` for tool approvals
- When receiving approval response via interrupt resume, continues execution

**Client/Program.cs**:
```csharp
// Connect to AGUI server
var client = new AGUIChatClient(httpClient, "http://localhost:5000/api/agent");

// Send message that triggers tool call
var response = await client.GetStreamingResponseAsync(messages, options);

// Handle interrupt
await foreach (var update in response)
{
    if (update.RawRepresentation is RunFinishedEvent { Outcome: "interrupt" } interruptEvent)
    {
        // Display approval request to user
        Console.WriteLine($"Approval requested: {interruptEvent.Interrupt?.Payload}");
        
        // Get user approval
        var approved = Console.ReadLine()?.ToLower() == "yes";
        
        // Resume with response
        options.AdditionalProperties["agui_resume"] = new AGUIResume
        {
            InterruptId = interruptEvent.Interrupt?.Id,
            Payload = JsonSerializer.SerializeToElement(new { approved })
        };
        
        // Continue the conversation
        response = await client.GetStreamingResponseAsync(messages, options);
    }
}
```

#### 6.2 Sample 2: User Input Request via Interrupts
**Location**: `samples/GettingStarted/AGUI/Step10_InterruptsUserInput/`

Demonstrates gathering additional information from user mid-execution using `UserInputRequestContent` with `RawRepresentation`:

**Server/UserInputAgent.cs**:
- Agent that requests additional user input during execution
- Uses `UserInputRequestContent` with payload stored in `RawRepresentation`
- Example: "Please provide your email address to continue"

**Server/Program.cs**:
```csharp
var agent = new UserInputRequestAgent(innerAgent);
app.MapAGUIAgent("/api/agent", agent);
```

**Client/Program.cs**:
```csharp
// Connect and start conversation
var client = new AGUIChatClient(httpClient, serverUrl);
var response = await client.GetStreamingResponseAsync(messages, options);

await foreach (var update in response)
{
    // Handle user input request interrupt (non-function-approval)
    if (update.Contents.OfType<UserInputRequestContent>().FirstOrDefault() is { } inputRequest
        && inputRequest is not FunctionApprovalRequestContent)
    {
        // Get the payload from RawRepresentation (AGUIInterrupt or JsonElement)
        if (inputRequest.RawRepresentation is AGUIInterrupt interrupt 
            && interrupt.Payload is { } payload)
        {
            var prompt = payload.GetProperty("prompt").GetString();
            Console.WriteLine($"Agent needs information: {prompt}");
        }
        
        Console.Write("Your response: ");
        var userInput = Console.ReadLine();
        
        // Create response and resume
        var inputResponse = new UserInputResponseContent(inputRequest.Id, userInput);
        messages.Add(new ChatMessage(ChatRole.User, [inputResponse]));
        
        // Continue
        response = await client.GetStreamingResponseAsync(messages, options);
    }
    else if (update.Contents.OfType<TextContent>().FirstOrDefault() is { } text)
    {
        Console.Write(text.Text);
    }
}
```

## File Changes Summary

### New Files

| File Path | Description |
|-----------|-------------|
| `src/AGUI.Protocol/Events/AGUIInterrupt.cs` | Interrupt payload type |
| `src/AGUI.Protocol/Events/RunFinishedOutcome.cs` | Outcome constants |
| `src/AGUI.Protocol/Context/AGUIResume.cs` | Resume payload type |
| `src/Microsoft.Agents.AI.AGUI/Shared/InterruptContentExtensions.cs` | Interrupt ↔ MEAI mappings |
| `tests/AGUI.Protocol.UnitTests/InterruptSerializationTests.cs` | Protocol serialization tests |
| `tests/Microsoft.Agents.AI.AGUI.UnitTests/InterruptMappingTests.cs` | Mapping tests |
| `tests/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests/InterruptTests.cs` | Integration tests |
| `samples/GettingStarted/AGUI/Step09_InterruptsApproval/Server/*` | Approval sample server |
| `samples/GettingStarted/AGUI/Step09_InterruptsApproval/Client/*` | Approval sample client |
| `samples/GettingStarted/AGUI/Step10_InterruptsUserInput/Server/*` | User input sample server |
| `samples/GettingStarted/AGUI/Step10_InterruptsUserInput/Client/*` | User input sample client |

### Modified Files

| File Path | Changes |
|-----------|---------|
| `src/AGUI.Protocol/Events/RunFinishedEvent.cs` | Add `Outcome`, `Interrupt` properties |
| `src/AGUI.Protocol/Context/RunAgentInput.cs` | Add `Resume` property |
| `src/AGUI.Protocol/Serialization/AGUIJsonSerializerContext.cs` | Register new types |
| `src/Microsoft.Agents.AI.AGUI/Shared/ChatResponseUpdateAGUIExtensions.cs` | Handle interrupt events |
| `src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIEndpointRouteBuilderExtensions.cs` | Handle resume in requests |
| `src/AGUI.Protocol/AGENTS.md` | Document new types |
| `src/Microsoft.Agents.AI.AGUI/AGENTS.md` | Document interrupt mappings |

## Implementation Order

1. **Protocol Types** (Phase 1) - Foundation types
2. **Unit Tests for Protocol** (Phase 4.1) - Verify serialization
3. **Mapping Extensions** (Phase 2) - Core translation logic
4. **Unit Tests for Mappings** (Phase 4.2) - Verify translations
5. **Hosting Integration** (Phase 3) - Server-side handling
6. **Integration Tests** (Phase 5) - End-to-end verification
7. **Sample 1: Approval** (Phase 6.1) - Approval workflow sample
8. **Sample 2: User Input** (Phase 6.2) - User input workflow sample

## Backward Compatibility

The implementation maintains backward compatibility:

1. **RunFinishedEvent.Outcome** is optional - existing events without outcome continue to work
2. **RunFinishedEvent.Result** presence indicates success (when outcome is absent)
3. **RunFinishedEvent.Interrupt** presence indicates interrupt (when outcome is absent)
4. **RunAgentInput.Resume** is optional - regular requests work unchanged

## Testing Strategy

Following the AG-UI spec's testing recommendations:

1. **Unit tests** for interrupt/resume serialization
2. **Integration tests** with server/client scenarios
3. **E2E tests** demonstrating:
   - Human approval flow
   - User input gathering
   - Multiple interrupt cycles
   - Error handling for invalid resume
4. **State consistency tests** across interrupt boundaries
5. **Performance tests** for rapid interrupt/resume cycles

## Dependencies

- Existing MEAI types: `FunctionApprovalRequestContent`, `FunctionApprovalResponseContent`, `UserInputRequestContent`, `UserInputResponseContent`
- Uses `AIContent.RawRepresentation` property (standard MEAI pattern) to store AG-UI interrupt payloads
- System.Text.Json for serialization
- No new external package dependencies
- No new custom content types required

## Open Questions

1. **Timeout handling**: Should interrupts have optional timeout configuration?
2. **Interrupt history**: Should we track interrupt/resume history in the session?
3. **Cancellation**: How should interrupt cancellation be handled?
4. **Additional content types**: Should we support additional specialized content types beyond `FunctionApprovalRequestContent` and `UserInputRequestContent`?

## References

- [AG-UI Interrupts Specification](https://docs.ag-ui.com/drafts/interrupts)
- [AG-UI Events Documentation](https://docs.ag-ui.com/concepts/events)
- [AG-UI State Management](https://docs.ag-ui.com/concepts/state)
- [Step04_HumanInLoop Sample](../samples/GettingStarted/AGUI/Step04_HumanInLoop) - Existing tool-based approach
