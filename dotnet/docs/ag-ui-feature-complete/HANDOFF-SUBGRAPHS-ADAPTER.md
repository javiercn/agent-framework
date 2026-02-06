# AG-UI Subgraphs Adapter - Handoff Document

## Goal

Enable the Microsoft Agent Framework's **spec-compliant workflow/RequestPort/Interrupt mechanism** to work with the **non-compliant AG-UI Dojo client** for the "Subgraphs" demo (multi-agent travel planner).

The AG-UI Dojo client uses a homemade human-in-the-loop mechanism that differs from the official AG-UI spec. We need an **adapter layer** that translates between the two formats without modifying the framework itself.

### Demo Flow
```
User: "Find me flights"
    → Supervisor routes to FlightsExecutor
    → FlightsExecutor finds flights, sends FlightSelectionRequest to RequestPort
    → RequestPort triggers interrupt, shows flight selection buttons to user
    → User clicks KLM flight button
    → Workflow resumes with selected Flight
    → Supervisor routes to HotelsExecutor
    → HotelsExecutor finds hotels, sends HotelSelectionRequest to RequestPort
    → User selects hotel
    → Supervisor routes to ExperiencesExecutor
    → ExperiencesExecutor finds activities
    → Supervisor routes to CompletionExecutor
    → Trip summary displayed
```

---

## What We Accomplished

### 1. Output Transformation (Working ✅)

**Problem:** The AG-UI Dojo client expects `CUSTOM` events with `name: "on_interrupt"` and a double-serialized JSON string value. The framework emits spec-compliant `RUN_FINISHED` events with `Interrupt` field.

**Solution:** Created `AGUIClientCompatibilityAdapter` that intercepts `RequestInfoEvent` and transforms it:

```csharp
// The adapter transforms:
// Framework: RequestInfoEvent → RUN_FINISHED with Interrupt
// Client expects: CUSTOM event { name: "on_interrupt", value: "{...escaped JSON string...}" }
```

Key implementation in [AGUIClientCompatibilityAdapter.cs](../samples/AGUIClientServer/AGUIDojoServer/Subgraphs/AGUIClientCompatibilityAdapter.cs):
- `TryTransformRequestInfoEvent()` - Intercepts RequestInfoEvent before hosting layer converts it
- `BuildCustomEventValue()` - Formats data for flight/hotel selection UI
- Double-serializes the value as LangGraph does (client expects JSON string, not object)

### 2. Input Transformation (Working ✅)

**Problem:** When user clicks a flight button, the AG-UI Dojo client sends the selection via `forwardedProps.command.resume` (a JSON string) instead of the proper AG-UI `Resume` field.

**Solution:** The adapter extracts this and creates a `FunctionResultContent`:

```csharp
// Client sends: forwardedProps: { command: { resume: "{\"airline\":\"KLM\",...}" } }
// Adapter creates: FunctionResultContent with RawRepresentation = ExternalRequest
```

Key implementation:
- `TransformInputMessages()` - Extracts resume from forwardedProps
- `s_pendingInterrupts` - Static dictionary tracking pending interrupts by threadId
- Attaches original `ExternalRequest` to `FunctionResultContent.RawRepresentation` for proper ExternalResponse creation

### 3. Session/Checkpoint Persistence (Working ✅)

**Problem:** Workflow state needs to persist between interrupt and resume requests.

**Solution:** Implemented `InMemoryAgentSessionStore` and proper checkpoint management:
- Sessions are saved with `LastCheckpoint` after each interrupt
- On resume, workflow resumes from checkpoint
- Chat history is preserved across requests

### 4. ExternalResponse Creation (Partially Working ⚠️)

**Problem:** When resuming, the workflow needs to receive an `ExternalResponse` with the properly typed data (e.g., `Flight` object, not `JsonElement`).

**Solution Attempted:** Modified `WorkflowSession.ProcessFunctionResultsAsExternalResponsesAsync()`:
- Extracts `ExternalRequest` from `FunctionResultContent.RawRepresentation`
- Uses `PortInfo.ResponseType` to get expected type
- Deserializes `JsonElement` to proper type with case-insensitive options
- Sends `ExternalResponse` to workflow via `run.SendResponseAsync()`

**Status:** The ExternalResponse IS being sent correctly with the proper `Flight` type. The logs show:
```
[EXTERNAL RESPONSE DEBUG] Deserialized to type Flight: {"Airline":"KLM",...}
[EXTERNAL RESPONSE DEBUG] ExternalResponse sent successfully!
[SUPERVISOR DEBUG] SelectedFlight: KLM  <- State IS updated correctly
[SUPERVISOR DEBUG] next_agent: hotels_agent  <- LLM correctly decides to go to hotels
```

---

## Remaining Problem

### Issue: Workflow Routes to FlightsExecutor Instead of HotelsExecutor

Despite the ExternalResponse being sent correctly and the Supervisor LLM deciding `hotels_agent`, the workflow still executes `FlightsExecutor`.

**Root Cause Analysis:**

The workflow architecture calls `TakeTurnAsync` BEFORE the route handlers process. When resuming:

1. `ResumeStreamAsync()` resumes the workflow from checkpoint
2. `TrySendMessageAsync(messages)` triggers `TakeTurnAsync()` on the Supervisor with **stale state** (no flight selected yet)
3. Supervisor's `TakeTurnAsync` calls `DetermineNextRouteAsync()` → LLM says "flights_agent"
4. **Then** the ExternalResponse triggers `HandleFlightSelectionAsync()` → state updated
5. Another `DetermineNextRouteAsync()` → LLM says "hotels_agent"
6. But the first routing decision (flights_agent) is already queued!

**Evidence from logs:**
```
[SUPERVISOR DEBUG] DetermineNextRouteAsync - hasFlights: False  <- First call with stale state
[SUPERVISOR DEBUG] next_agent: flights_agent

[SUPERVISOR DEBUG] DetermineNextRouteAsync - hasFlights: True, SelectedFlight: KLM  <- Second call after route handler
[SUPERVISOR DEBUG] next_agent: hotels_agent
```

**SSE events show:**
```
STEP_STARTED: Supervisor
STEP_STARTED: SelectFlight  <- ExternalResponse processed
STEP_FINISHED: SelectFlight
STEP_FINISHED: Supervisor
STEP_STARTED: Supervisor
STEP_STARTED: FlightsExecutor  <- Wrong! Should be HotelsExecutor
STEP_FINISHED: FlightsExecutor
```

### Attempted Fix That Didn't Work

We tried to skip sending messages via `TrySendMessageAsync` when ExternalResponses were processed:

```csharp
bool processedExternalResponses = await ProcessFunctionResultsAsExternalResponsesAsync(...);
if (!processedExternalResponses)
{
    await checkpointed.Run.TrySendMessageAsync(messages);
}
```

This didn't work because the issue is deeper in the workflow architecture - the routing decision from TakeTurnAsync is made before the route handler runs.

---

## Files Modified

### Production Code (src/)
1. **`WorkflowSession.cs`** - No permanent changes made (debug logging was removed)

### Sample Code (samples/)
1. **`AGUIClientCompatibilityAdapter.cs`** - Full adapter implementation
2. **`TravelAgentWorkflowFactory.cs`** - Supervisor-pattern workflow with RequestPorts
3. **`InMemoryAgentSessionStore.cs`** - Session persistence for checkpoint management
4. **`TravelAgentModels.cs`** - Flight, Hotel, Experience, SelectionRequest types
5. **`TravelData.cs`** - Static sample data

---

## Recommended Next Steps

### Option 1: Modify ChatProtocolExecutor (Framework Change)
Add a mechanism for executors to detect when resuming from an interrupt and skip `TakeTurnAsync` routing. The route handler should be the primary execution path on resume.

### Option 2: Redesign Supervisor Pattern
Change the workflow architecture so that:
- On resume, the workflow goes directly to the port's downstream edge (Supervisor)
- The Supervisor's route handler processes first
- Then it decides the next route based on updated state

### Option 3: State-Based Skip in TakeTurnAsync
Add logic to `SupervisorExecutor.TakeTurnAsync()` to detect when the workflow just received an ExternalResponse and should skip routing (let the route handler do it):

```csharp
protected override async ValueTask TakeTurnAsync(...)
{
    // If we're resuming from an interrupt, the route handler will call DetermineNextRouteAsync
    // Don't route here to avoid duplicate/stale routing
    if (IsResumingFromInterrupt(messages))
    {
        return; // Skip - let HandleFlightSelectionAsync do the routing
    }
    // ... existing routing logic
}
```

### Option 4: Defer Routing to Route Handler Only
Move ALL routing logic to the route handlers (`HandleFlightSelectionAsync`, `HandleHotelSelectionAsync`) and have `TakeTurnAsync` only handle the initial user message (not resume messages).

---

## Test Environment Setup

1. **Server:** `samples/AGUIClientServer/AGUIDojoServer` on `http://localhost:5018`
2. **Client:** AG-UI Dojo at `http://localhost:3002/microsoft-agent-framework-dotnet/feature/subgraphs`
3. **Azure OpenAI:** 
   - Endpoint: `https://ag-ui-agent-framework.openai.azure.com/`
   - Deployment: `gpt-4.1-mini`

### Running the Test
```powershell
# Terminal 1: Start Dojo client
cd .tmp/ag-ui/apps/dojo
pnpm dev

# Terminal 2: Start .NET server
cd samples/AGUIClientServer/AGUIDojoServer
$env:AZURE_OPENAI_ENDPOINT = "https://ag-ui-agent-framework.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT_NAME = "gpt-4.1-mini"
dotnet run --framework net10.0
```

### Test Steps
1. Navigate to `http://localhost:3002/microsoft-agent-framework-dotnet/feature/subgraphs`
2. Type "find me flights" and click Send
3. Wait for flight options to appear (KLM and United buttons)
4. Click KLM button
5. **Expected:** Hotel selection UI should appear
6. **Actual:** UI returns to initial state, workflow routes back to flights

---

## Key Insights

1. **The adapter IS working correctly** - it transforms events in both directions
2. **ExternalResponse IS being sent** with proper type
3. **The Supervisor LLM IS making the right decision** (hotels_agent) in the second call
4. **The problem is execution ordering** - TakeTurnAsync runs before route handlers
5. **This is a workflow architecture issue**, not an adapter issue

---

## Reference: LangGraph Comparison

The LangGraph TypeScript implementation at `https://dojo.ag-ui.com/langgraph-typescript/feature/subgraphs` works correctly because:
1. It uses a different workflow architecture
2. Its interrupt mechanism doesn't have the TakeTurnAsync/RouteHandler ordering issue
3. The resume goes directly to the correct subgraph without re-routing through the supervisor with stale state

---

## Contact

For questions about this work, reference the GitHub branch: `javiercn/agui-packages-refactor`
