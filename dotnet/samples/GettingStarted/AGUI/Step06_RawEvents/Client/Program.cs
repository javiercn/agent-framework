// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

// This sample demonstrates how to detect and consume raw AG-UI events
// that were emitted by an agent via RawRepresentation.

Console.WriteLine("AG-UI Raw Events Client Sample");
Console.WriteLine("==============================");
Console.WriteLine();

// Create an AG-UI chat client pointing to the local server
using var httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost:5000") };
IChatClient chatClient = new AGUIChatClient(httpClient, "/");

// Simulate state that the client tracks
var clientState = new Dictionary<string, object>
{
    ["counter"] = 0,
    ["lastAction"] = "none"
};

// Messages to send that will trigger state changes
string[] testMessages =
[
    "Hello, what can you do?",
    "Please increment the counter",
    "Add one more",
    "Now decrement it",
    "Reset the counter to zero"
];

foreach (string message in testMessages)
{
    Console.WriteLine($"User: {message}");
    Console.WriteLine();

    // Create chat options with the current state
    // This is how AG-UI clients pass state to the agent
    var options = new ChatOptions
    {
        AdditionalProperties = new AdditionalPropertiesDictionary
        {
            ["ag_ui_state"] = JsonSerializer.SerializeToElement(clientState)
        }
    };

    // Stream the response and detect raw AG-UI events
    await foreach (var update in chatClient.GetStreamingResponseAsync(message, options))
    {
        // Check if this update contains a raw AG-UI event
        if (update.RawRepresentation is BaseEvent aguiEvent)
        {
            // The client can now handle different AG-UI event types directly
            switch (aguiEvent)
            {
                case StateSnapshotEvent stateSnapshot:
                    Console.WriteLine("  [State Snapshot Received]");
                    if (stateSnapshot.Snapshot is { } snapshot)
                    {
                        Console.WriteLine($"    Raw JSON: {snapshot}");

                        // Update our local state from the snapshot
                        if (snapshot.TryGetProperty("counter", out var counterElement))
                        {
                            clientState["counter"] = counterElement.GetInt32();
                        }
                        if (snapshot.TryGetProperty("lastAction", out var actionElement))
                        {
                            clientState["lastAction"] = actionElement.GetString() ?? "none";
                        }

                        Console.WriteLine($"    Local state updated: counter={clientState["counter"]}, lastAction={clientState["lastAction"]}");
                    }
                    break;

                case StateDeltaEvent stateDelta:
                    Console.WriteLine("  [State Delta Received]");
                    Console.WriteLine($"    Delta: {string.Join(", ", stateDelta.Delta)}");
                    // In a real application, you would apply the JSON Patch operations
                    break;

                case RunStartedEvent:
                    Console.WriteLine("  [Run Started]");
                    break;

                case RunFinishedEvent:
                    Console.WriteLine("  [Run Finished]");
                    break;

                case RunErrorEvent errorEvent:
                    Console.WriteLine($"  [Error: {errorEvent.Message}]");
                    break;

                case TextMessageContentEvent textContent:
                    // Text content is streamed via these events
                    Console.Write(textContent.Delta);
                    break;

                case ToolCallStartEvent toolStart:
                    Console.WriteLine($"  [Tool Call Start: {toolStart.ToolCallName}]");
                    break;

                case ToolCallEndEvent:
                    Console.WriteLine("  [Tool Call End]");
                    break;

                default:
                    Console.WriteLine($"  [Other AG-UI Event: {aguiEvent.GetType().Name}]");
                    break;
            }
        }
        else
        {
            // Regular M.E.AI update without raw AG-UI event
            // This happens when the agent doesn't set RawRepresentation
            if (update.Text is { Length: > 0 } text)
            {
                Console.Write(text);
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine($"Current state: counter={clientState["counter"]}, lastAction={clientState["lastAction"]}");
    Console.WriteLine();
    Console.WriteLine(new string('-', 60));
    Console.WriteLine();
}

Console.WriteLine("Demo complete!");
