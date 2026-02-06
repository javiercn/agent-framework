// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

string serverUrl = Environment.GetEnvironmentVariable("AGUI_SERVER_URL") ?? "http://localhost:8888";

Console.WriteLine("AG-UI Serialization Sample - Client");
Console.WriteLine("====================================");
Console.WriteLine();
Console.WriteLine("This sample demonstrates two serialization dimensions:");
Console.WriteLine("1. Client-side event stream serialization (for history persistence)");
Console.WriteLine("2. Server-side session persistence (via threadId)");
Console.WriteLine();
Console.WriteLine($"Connecting to AG-UI server at: {serverUrl}");
Console.WriteLine();

// Create the AG-UI client agent
using HttpClient httpClient = new()
{
    Timeout = TimeSpan.FromSeconds(60)
};

AGUIChatClient chatClient = new(httpClient, serverUrl);

AIAgent agent = chatClient.AsAIAgent(
    name: "agui-serialization-client",
    description: "AG-UI Client demonstrating serialization");

AgentSession session = await agent.GetNewSessionAsync();
List<ChatMessage> messages =
[
    new(ChatRole.System, "You are a helpful assistant.")
];

// Use a consistent threadId for this conversation
// The server will maintain session state for this threadId
string threadId = Guid.NewGuid().ToString("N");
Console.WriteLine($"Thread ID: {threadId}");
Console.WriteLine("(Using consistent threadId enables server-side session persistence)");
Console.WriteLine();

// Collect all events for later serialization
List<BaseEvent> allEvents = [];

try
{
    while (true)
    {
        Console.Write("\nUser (:q to quit, :save to save events, :load to load events): ");
        string? message = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(message))
        {
            Console.WriteLine("Request cannot be empty.");
            continue;
        }

        if (message is ":q" or "quit")
        {
            break;
        }

        // Handle save command - serialize events to file
        if (message == ":save")
        {
            SaveEventsToFile(allEvents);
            continue;
        }

        // Handle load command - deserialize events from file
        if (message == ":load")
        {
            var loadedEvents = LoadEventsFromFile();
            if (loadedEvents != null)
            {
                allEvents = loadedEvents;
                Console.WriteLine($"Loaded {allEvents.Count} events from history.json");
            }
            continue;
        }

        messages.Add(new ChatMessage(ChatRole.User, message));

        // Stream the response
        Console.WriteLine();

        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session))
        {
            // Capture AG-UI events for serialization
            BaseEvent? evt = GetAGUIEvent(update);
            if (evt != null)
            {
                allEvents.Add(evt);
            }

            // Display lifecycle events
            switch (evt)
            {
                case RunStartedEvent runStarted:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Run Started - Session: {runStarted.ThreadId}, Run: {runStarted.RunId}]");
                    Console.ResetColor();
                    continue;

                case RunFinishedEvent runFinished:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n[Run Finished - Session: {runFinished.ThreadId}]");
                    Console.ResetColor();
                    continue;

                case RunErrorEvent runError:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[Run Error: {runError.Message}]");
                    Console.ResetColor();
                    continue;
            }

            // Display streaming text content
            foreach (AIContent content in update.Contents)
            {
                if (content is TextContent textContent)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(textContent.Text);
                    Console.ResetColor();
                }
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"(Total events captured: {allEvents.Count})");
        Console.ResetColor();
    }

    // Auto-save on exit if there are events
    if (allEvents.Count > 0)
    {
        Console.Write("\nSave events before exiting? (y/n): ");
        if (string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase))
        {
            SaveEventsToFile(allEvents);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\nAn error occurred: {ex.Message}");
}

// =====================
// Helper Functions
// =====================

static void SaveEventsToFile(List<BaseEvent> events)
{
    const string filename = "history.json";
    try
    {
        // Use AGUIJsonSerializerContext for AOT-compatible serialization
        string json = JsonSerializer.Serialize(events.ToArray(), AGUIJsonSerializerContext.Default.Options);

        // Pretty print for readability
        using var doc = JsonDocument.Parse(json);
        string prettyJson = doc.RootElement.ToString();

        File.WriteAllText(filename, prettyJson);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Saved {events.Count} events to {filename}");
        Console.ResetColor();

        // Show summary of saved events
        var eventCounts = events.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count());
        Console.WriteLine("Event summary:");
        foreach (var kvp in eventCounts.OrderBy(k => k.Key))
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Failed to save events: {ex.Message}");
        Console.ResetColor();
    }
}

static List<BaseEvent>? LoadEventsFromFile()
{
    const string filename = "history.json";
    try
    {
        if (!File.Exists(filename))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"File {filename} not found.");
            Console.ResetColor();
            return null;
        }

        string json = File.ReadAllText(filename);

        // Use AGUIJsonSerializerContext for AOT-compatible deserialization
        var events = JsonSerializer.Deserialize<BaseEvent[]>(json, AGUIJsonSerializerContext.Default.Options);

        if (events == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No events found in file.");
            Console.ResetColor();
            return null;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Successfully loaded {events.Length} events from {filename}");
        Console.ResetColor();

        // Show summary of loaded events
        var eventCounts = events.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count());
        Console.WriteLine("Event summary:");
        foreach (var kvp in eventCounts.OrderBy(k => k.Key))
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        return [.. events];
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Failed to load events: {ex.Message}");
        Console.ResetColor();
        return null;
    }
}

static BaseEvent? GetAGUIEvent(AgentResponseUpdate update)
{
    return FindBaseEvent(update.RawRepresentation);

    static BaseEvent? FindBaseEvent(object? obj)
    {
        return obj switch
        {
            BaseEvent baseEvent => baseEvent,
            AgentResponseUpdate aru => FindBaseEvent(aru.RawRepresentation),
            ChatResponseUpdate cru => FindBaseEvent(cru.RawRepresentation),
            _ => null
        };
    }
}
