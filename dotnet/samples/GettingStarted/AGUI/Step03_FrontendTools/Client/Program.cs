// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using AGUI.Protocol;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

string serverUrl = Environment.GetEnvironmentVariable("AGUI_SERVER_URL") ?? "http://localhost:8888";

// Support non-interactive mode via command line argument
string? singleMessage = args.Length > 0 ? string.Join(" ", args) : null;
bool interactiveMode = singleMessage == null;

Console.WriteLine($"Connecting to AG-UI server at: {serverUrl}\n");

// Define a frontend function tool
[Description("Get the user's current location from GPS.")]
static string GetUserLocation()
{
    // Access client-side GPS
    return "Amsterdam, Netherlands (52.37°N, 4.90°E)";
}

// Create frontend tools
AITool[] frontendTools = [AIFunctionFactory.Create(GetUserLocation)];

// Create the AG-UI client agent with tools
using HttpClient httpClient = new()
{
    Timeout = TimeSpan.FromSeconds(60)
};

AGUIChatClient chatClient = new(httpClient, serverUrl);

AIAgent agent = chatClient.AsAIAgent(
    name: "agui-client",
    description: "AG-UI Client Agent",
    tools: frontendTools);

AgentSession session = await agent.CreateSessionAsync();
List<ChatMessage> messages =
[
    new(ChatRole.System, "You are a helpful assistant.")
];

try
{
    while (true)
    {
        // Get user input (or use command line argument in non-interactive mode)
        string? message;
        if (interactiveMode)
        {
            Console.Write("\nUser (:q or quit to exit): ");
            message = Console.ReadLine();
        }
        else
        {
            message = singleMessage;
            Console.WriteLine($"User: {message}");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            Console.WriteLine("Request cannot be empty.");
            if (!interactiveMode)
            {
                break;
            }
            continue;
        }

        if (message is ":q" or "quit")
        {
            break;
        }

        messages.Add(new ChatMessage(ChatRole.User, message));

        // Stream the response
        Console.WriteLine();

        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session))
        {
            // Check for AG-UI lifecycle events via RawRepresentation
            // The RawRepresentation may contain an AgentResponseUpdate -> ChatResponseUpdate -> BaseEvent chain
            switch (GetAGUIEvent(update))
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

            // Display streaming content
            foreach (AIContent content in update.Contents)
            {
                if (content is TextContent textContent)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(textContent.Text);
                    Console.ResetColor();
                }
                else if (content is FunctionCallContent functionCallContent)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n[Client Tool Call - Name: {functionCallContent.Name}]");
                    Console.ResetColor();
                }
                else if (content is FunctionResultContent functionResultContent)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[Client Tool Result: {functionResultContent.Result}]");
                    Console.ResetColor();
                }
                else if (content is ErrorContent errorContent)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[Error: {errorContent.Message}]");
                    Console.ResetColor();
                }
            }
        }

        // Exit after single message in non-interactive mode
        if (!interactiveMode)
        {
            break;
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\nAn error occurred: {ex.Message}");
}

// Helper function to extract AG-UI BaseEvent from the RawRepresentation chain
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
