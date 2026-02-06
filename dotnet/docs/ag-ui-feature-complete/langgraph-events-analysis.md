# LangGraph AG-UI Events Analysis

This document analyzes the AG-UI events emitted by the LangGraph TypeScript implementation
for the "subgraphs" travel planning demo. These events were captured from the network
response when running the demo at https://dojo.ag-ui.com/langgraph-typescript/feature/subgraphs.

## Event Types Summary

| Event Type | Count | Description |
|------------|-------|-------------|
| RUN_STARTED | 1 | Marks the beginning of a run |
| STEP_STARTED | 6 | Marks when a step/agent starts |
| STEP_FINISHED | 6 | Marks when a step/agent completes |
| STATE_SNAPSHOT | 15 | State updates during execution |
| RAW | 67 | Raw LangGraph internal events |
| TOOL_CALL_START | 1 | Tool call initiation |
| TOOL_CALL_ARGS | 51 | Streaming tool call arguments |
| TOOL_CALL_END | 1 | Tool call completion |
| **CUSTOM** | 1 | **Custom events for UI rendering (key for buttons)** |
| MESSAGES_SNAPSHOT | 1 | Final messages snapshot |
| RUN_FINISHED | 1 | Marks run completion |

## Key Findings

### 1. User Selection Mechanism via CUSTOM Events

LangGraph uses a **`CUSTOM`** event type (not RUN_FINISHED with interrupt) to send
options to the frontend for user selection:

```json
{
  "type": "CUSTOM",
  "name": "on_interrupt",
  "value": "{\"message\":\"Found 3 accommodation options...\",\"options\":[...],\"recommendation\":{...},\"agent\":\"hotels\"}",
  "rawEvent": { ... }
}
```

The `value` field contains JSON with:
- `message`: Text message to display to the user
- `options`: Array of selectable options
- `recommendation`: The recommended option (highlighted in UI)
- `agent`: Which agent is sending the options

### 2. User Selection Response

When the user clicks a button, the frontend sends a new request with the selected
option in `forwardedProps.command.resume`:

```json
{
  "type": "RUN_STARTED",
  "input": {
    "forwardedProps": {
      "command": {
        "resume": "{\"airline\":\"KLM\",\"departure\":\"Amsterdam (AMS)\",...}"
      }
    }
  }
}
```

### 3. Step Events

Step events use `stepName` only (no `stepId`):

```json
{"type": "STEP_STARTED", "stepName": "flights_agent"}
{"type": "STEP_FINISHED", "stepName": "flights_agent"}
```

### 4. Event Flow Example

1. `RUN_STARTED` - Run begins with user message
2. `STEP_STARTED` stepName="supervisor" - Supervisor starts
3. `STATE_SNAPSHOT` - State update
4. `STEP_FINISHED` stepName="supervisor" - Supervisor routes to flights
5. `STEP_STARTED` stepName="flights_agent" - Flights agent starts
6. `STATE_SNAPSHOT` with RAW events - Agent processing
7. `CUSTOM` name="on_interrupt" - **Flights options sent to UI**
8. `STEP_FINISHED` stepName="flights_agent"
9. `RUN_FINISHED` - Run pauses waiting for user selection

After user selects a flight:
1. `RUN_STARTED` with `command.resume` containing selected flight
2. Flow continues...

## Sample CUSTOM Event (Hotels)

```json
{
  "type": "CUSTOM",
  "name": "on_interrupt",
  "value": {
    "message": "Found 3 accommodation options in San Francisco.\n\n    I recommend choosing the Hotel Zoe since it strikes the balance between rating, price, and location.",
    "options": [
      {
        "name": "Hotel Zephyr",
        "location": "Fisherman's Wharf",
        "price_per_night": "$280/night",
        "rating": "4.2 stars"
      },
      {
        "name": "The Ritz-Carlton",
        "location": "Nob Hill",
        "price_per_night": "$550/night",
        "rating": "4.8 stars"
      },
      {
        "name": "Hotel Zoe",
        "location": "Union Square",
        "price_per_night": "$320/night",
        "rating": "4.4 stars"
      }
    ],
    "recommendation": {
      "name": "Hotel Zoe",
      "location": "Union Square",
      "price_per_night": "$320/night",
      "rating": "4.4 stars"
    },
    "agent": "hotels"
  }
}
```

## Implications for Microsoft Agent Framework Implementation

To achieve the same UX as LangGraph's subgraphs demo, the Agent Framework needs to:

1. **Emit CUSTOM events** with `name: "on_interrupt"` when the workflow needs user input
2. **Structure the value** with `message`, `options`, `recommendation`, and `agent` fields
3. **Handle resume commands** from `forwardedProps.command.resume` to continue workflows
4. **Convert workflow RequestPort interrupts** to these CUSTOM events

### DelegatingAgent Responsibilities

Create a `DelegatingAgent` that wraps the workflow and:

1. Intercepts `RUN_FINISHED` events with interrupt payloads
2. Transforms interrupt data into `CUSTOM` events with the expected format
3. Handles incoming `command.resume` to continue the workflow
4. Passes through other events unchanged (STEP_STARTED, STEP_FINISHED, etc.)

## Implementation: AGUIClientCompatibilityAdapter

The `AGUIClientCompatibilityAdapter` class (in `samples/AGUIClientServer/AGUIDojoServer/Subgraphs/`) 
implements this adapter pattern. It extends `DelegatingAIAgent` to wrap the workflow agent.

### Input Transformation

The adapter extracts `forwardedProps.command.resume` from the incoming request and sets it
as `RunAgentInput.Resume`:

```csharp
// Check if forwardedProps.command.resume exists
if (aguiInput.ForwardedProperties.ValueKind == JsonValueKind.Object &&
    aguiInput.ForwardedProperties.TryGetProperty("command", out var command) &&
    command.ValueKind == JsonValueKind.Object &&
    command.TryGetProperty("resume", out var resume))
{
    // Parse and set as Resume
    aguiInput.Resume = new AGUIResume
    {
        InterruptId = TryExtractInterruptId(resumePayload),
        Payload = resumePayload
    };
}
```

### Output Transformation

When the workflow emits a `RunFinishedEvent` with an `Interrupt`, the adapter:

1. Extracts the interrupt payload (which contains `FlightSelectionRequest` or `HotelSelectionRequest`)
2. Builds a `CUSTOM` event with the client-expected format (`message`, `options`, `recommendation`, `agent`)
3. Emits the `CUSTOM` event followed by a regular `RUN_FINISHED` (without interrupt)

```csharp
// Create the CUSTOM event
var aguiCustomEvent = new CustomEvent
{
    Name = "on_interrupt",
    Value = customValue  // { message, options, recommendation, agent }
};

// Create regular RUN_FINISHED (without interrupt payload)
var regularFinished = new RunFinishedEvent
{
    ThreadId = finishedEvent.ThreadId,
    RunId = finishedEvent.RunId,
    Outcome = RunFinishedOutcome.Interrupt
};
```

### Usage

The adapter is wired up in `ChatClientAgentFactory.CreateSubgraphs()`:

```csharp
public static AIAgent CreateSubgraphs(JsonSerializerOptions options)
{
    ChatClient chatClient = s_azureOpenAIClient!.GetChatClient(s_deploymentName!);
    
    // Create the travel agent workflow
    var workflowAgent = TravelAgentWorkflowFactory.Create(chatClient.AsIChatClient()).AsAgent();
    
    // Wrap with the compatibility adapter for AG-UI dojo client
    return new AGUIClientCompatibilityAdapter(workflowAgent, options);
}
```

This approach keeps the framework spec-compliant while handling the non-compliant client
behavior at the sample/adapter level.
