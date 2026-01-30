// Copyright (c) Microsoft. All rights reserved.

// This sample demonstrates the native AG-UI interrupts pattern for user input requests.
// When the agent needs additional information, the server emits RUN_FINISHED events
// with outcome="interrupt" and a UserInputRequestContent payload.
// The client handles the interrupt, prompts the user, and sends back a resume payload.

using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

#pragma warning disable MEAI001 // Experimental API - UserInputRequestContent

string serverUrl = Environment.GetEnvironmentVariable("AGUI_SERVER_URL") ?? "http://localhost:5110";

using HttpClient httpClient = new()
{
    Timeout = TimeSpan.FromSeconds(60)
};

AGUIChatClient chatClient = new(httpClient, serverUrl);
AIAgent agent = chatClient.AsAIAgent(
    name: "UserInputAgent",
    instructions: "You are a helpful assistant.");

List<ChatMessage> messages = [];
AgentSession? session = await agent.GetNewSessionAsync();

Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine("AG-UI User Input Interrupts Sample");
Console.WriteLine("===================================");
Console.WriteLine("This sample demonstrates requesting user input via AG-UI interrupts.");
Console.WriteLine("Try saying \"setup my account\" or \"update my email\".");
Console.WriteLine("Type 'exit' to quit.\n");
Console.ResetColor();

string? input;
while ((input = Console.ReadLine()) != null && !input.Equals("exit", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    messages.Add(new ChatMessage(ChatRole.User, input));
    Console.WriteLine();

    // Process response, handling interrupts
    await ProcessAgentResponseAsync(agent, messages, session);

    Console.WriteLine("\n");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("Ask another question (or type 'exit' to quit):");
    Console.ResetColor();
}

async Task ProcessAgentResponseAsync(AIAgent agent, List<ChatMessage> messages, AgentSession? session)
{
    while (true)
    {
        UserInputRequestContent? pendingInputRequest = null;
        List<AgentResponseUpdate> updates = [];

        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session, cancellationToken: default))
        {
            updates.Add(update);

            foreach (AIContent content in update.Contents)
            {
                switch (content)
                {
                    case UserInputRequestContent inputRequest when inputRequest is not FunctionApprovalRequestContent:
                        // The server has emitted an interrupt requesting user input
                        pendingInputRequest = inputRequest;
                        DisplayUserInputRequest(inputRequest);
                        break;

                    case TextContent textContent:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write(textContent.Text);
                        Console.ResetColor();
                        break;

                    case ErrorContent error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[Error: {error.Message}]");
                        Console.ResetColor();
                        break;
                }
            }
        }

        // Add assistant messages to history
        AgentResponse response = updates.ToAgentResponse();
        messages.AddRange(response.Messages);

        // If there was an input request, handle it
        if (pendingInputRequest is not null)
        {
            Console.Write("\nYour input: ");
            string? userInput = Console.ReadLine();

            // Create the input response with the user's data
            // Use a local test class since UserInputResponseContent has protected constructor
            var inputResponse = new TestUserInputResponseContent(pendingInputRequest.Id)
            {
                RawRepresentation = JsonDocument.Parse($$"""{"response":"{{userInput?.Replace("\"", "\\\"") ?? ""}}"}""").RootElement
            };

            // Add as a user message with the input response content
            // The framework will convert this to an AGUIResume when sending to the server
            messages.Add(new ChatMessage(ChatRole.User, [inputResponse]));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[Input submitted - continuing execution]\n");
            Console.ResetColor();

            // Continue the loop to process the resumed agent execution
            continue;
        }

        // No pending input request, we're done
        break;
    }
}

static void DisplayUserInputRequest(UserInputRequestContent inputRequest)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║           USER INPUT REQUIRED (via Interrupt)            ║");
    Console.WriteLine("╠══════════════════════════════════════════════════════════╣");

    // Extract prompt and input type from RawRepresentation
    string prompt = "Please provide input";
    string inputType = "text";

    if (inputRequest.RawRepresentation is AGUIInterrupt interrupt && interrupt.Payload.HasValue)
    {
        var payload = interrupt.Payload.Value;
        if (payload.TryGetProperty("prompt", out var promptElement))
        {
            prompt = promptElement.GetString() ?? prompt;
        }
        if (payload.TryGetProperty("inputType", out var typeElement))
        {
            inputType = typeElement.GetString() ?? inputType;
        }
    }
    else if (inputRequest.RawRepresentation is JsonElement json)
    {
        if (json.TryGetProperty("prompt", out var promptElement))
        {
            prompt = promptElement.GetString() ?? prompt;
        }
        if (json.TryGetProperty("inputType", out var typeElement))
        {
            inputType = typeElement.GetString() ?? inputType;
        }
    }

    Console.WriteLine($"║ Prompt: {prompt,-48} ║");
    Console.WriteLine($"║ Type: {inputType,-50} ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
    Console.ResetColor();
}

/// <summary>
/// Test implementation of UserInputResponseContent that can be instantiated directly.
/// </summary>
internal sealed class TestUserInputResponseContent : UserInputResponseContent
{
    public TestUserInputResponseContent(string id) : base(id)
    {
    }
}
