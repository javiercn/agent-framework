// Copyright (c) Microsoft. All rights reserved.

// This sample demonstrates the native AG-UI interrupts pattern for function approval.
// Instead of using synthetic tool calls (like Step04_HumanInLoop), the server emits
// RUN_FINISHED events with outcome="interrupt" when tools require approval.
// The client handles FunctionApprovalRequestContent and sends back a resume payload.

using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

#pragma warning disable MEAI001 // Experimental API - FunctionApprovalRequestContent

string serverUrl = Environment.GetEnvironmentVariable("AGUI_SERVER_URL") ?? "http://localhost:5109";

using HttpClient httpClient = new()
{
    Timeout = TimeSpan.FromSeconds(60)
};

AGUIChatClient chatClient = new(httpClient, serverUrl);
AIAgent agent = chatClient.AsAIAgent(
    name: "ApprovalAgent",
    instructions: "You are a helpful assistant.");

List<ChatMessage> messages = [];
AgentSession? session = await agent.GetNewSessionAsync();

Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine("AG-UI Interrupts Approval Sample");
Console.WriteLine("================================");
Console.WriteLine("Ask the agent to delete a file or transfer money to see approval interrupts.");
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
        FunctionApprovalRequestContent? pendingApproval = null;
        List<AgentResponseUpdate> updates = [];

        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session, cancellationToken: default))
        {
            updates.Add(update);

            foreach (AIContent content in update.Contents)
            {
                switch (content)
                {
                    case FunctionApprovalRequestContent approvalRequest:
                        // The server has emitted an interrupt requesting approval
                        pendingApproval = approvalRequest;
                        DisplayApprovalRequest(approvalRequest);
                        break;

                    case TextContent textContent:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write(textContent.Text);
                        Console.ResetColor();
                        break;

                    case FunctionCallContent functionCall:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[Tool Call - Name: {functionCall.Name}]");
                        if (functionCall.Arguments is { } arguments)
                        {
                            Console.WriteLine($"  Parameters: {JsonSerializer.Serialize(arguments)}");
                        }
                        Console.ResetColor();
                        break;

                    case FunctionResultContent functionResult:
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"[Tool Result: {functionResult.Result}]");
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

        // If there was an approval request, handle it
        if (pendingApproval is not null)
        {
            Console.Write($"\nApprove '{pendingApproval.FunctionCall.Name}'? (yes/no): ");
            string? userInput = Console.ReadLine();
            bool approved = userInput?.ToUpperInvariant() is "YES" or "Y";

            // Create the approval response
            FunctionApprovalResponseContent approvalResponse = pendingApproval.CreateResponse(approved);

            // Add as a user message with the approval response content
            // The framework will convert this to an AGUIResume when sending to the server
            messages.Add(new ChatMessage(ChatRole.User, [approvalResponse]));

            Console.ForegroundColor = approved ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(approved ? "[Approved - continuing execution]" : "[Rejected - action cancelled]");
            Console.ResetColor();

            // Continue the loop to process the resumed agent execution
            continue;
        }

        // No pending approval, we're done
        break;
    }
}

static void DisplayApprovalRequest(FunctionApprovalRequestContent approvalRequest)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║               APPROVAL REQUIRED (via Interrupt)          ║");
    Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║ Function: {approvalRequest.FunctionCall.Name,-47} ║");

    if (approvalRequest.FunctionCall.Arguments != null)
    {
        Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ Arguments:                                               ║");
        foreach (var arg in approvalRequest.FunctionCall.Arguments)
        {
            string argLine = $"   {arg.Key} = {arg.Value}";
            Console.WriteLine($"║ {argLine,-55} ║");
        }
    }

    Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
    Console.ResetColor();
}
