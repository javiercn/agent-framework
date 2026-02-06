// Copyright (c) Microsoft. All rights reserved.

using AGUI.Protocol;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

string serverUrl = Environment.GetEnvironmentVariable("AGUI_SERVER_URL") ?? "http://localhost:8888";

// Support non-interactive mode via command line argument
string? singleMessage = args.Length > 0 ? string.Join(" ", args) : null;
bool interactiveMode = singleMessage == null;

Console.WriteLine($"Connecting to AG-UI server at: {serverUrl}");
Console.WriteLine("This sample demonstrates reasoning events from reasoning models (e.g., gpt-5.1-codex-mini).\n");
Console.WriteLine("Reasoning content appears in magenta, response content in cyan.\n");

// Create the AG-UI client agent
using HttpClient httpClient = new()
{
    Timeout = TimeSpan.FromSeconds(120) // Longer timeout for reasoning models
};

AGUIChatClient chatClient = new(httpClient, serverUrl);

AIAgent agent = chatClient.AsAIAgent(
    name: "agui-reasoning-client",
    description: "AG-UI Client for Reasoning Events");

AgentSession session = await agent.GetNewSessionAsync();
List<ChatMessage> messages =
[
    new(ChatRole.System, "You are a helpful reasoning assistant. Show your thinking process step by step.")
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

            // Process AIContent - the MEAI abstraction layer
            // TextReasoningContent represents "reasoning" content from reasoning models
            // TextContent represents the actual response content
            foreach (AIContent content in update.Contents)
            {
                if (content is TextReasoningContent reasoningContent)
                {
                    // TextReasoningContent is the MEAI abstraction for thinking/reasoning
                    // This is mapped from AG-UI ReasoningMessageContentEvent (or legacy ThinkingTextMessageContentEvent)
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write(reasoningContent.Text);
                    Console.ResetColor();
                }
                else if (content is TextContent textContent)
                {
                    // TextContent is the actual response from the model
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(textContent.Text);
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
    Console.WriteLine(ex.StackTrace);
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
