// Copyright (c) Microsoft. All rights reserved.

// This sample demonstrates the native AG-UI interrupts pattern for function approval.
// The agent emits FunctionApprovalRequestContent which the framework automatically
// converts to a RUN_FINISHED event with outcome="interrupt".

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;

#pragma warning disable MEAI001 // Experimental API - FunctionApprovalRequestContent

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient().AddLogging();
builder.Services.AddAGUI();

WebApplication app = builder.Build();

// Create a custom agent that demonstrates function approval via interrupts
var agent = new FunctionApprovalAgent();

// Map the AG-UI endpoint - interrupts are handled automatically by the framework
app.MapAGUI("/", agent);

await app.RunAsync();

/// <summary>
/// A sample agent that demonstrates function approval via AG-UI interrupts.
/// When asked to perform sensitive operations, the agent emits FunctionApprovalRequestContent
/// which the framework converts to a RUN_FINISHED event with outcome="interrupt".
/// </summary>
internal sealed class FunctionApprovalAgent : AIAgent
{
    public override string? Description => "Agent that requests function approval via interrupts";

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return RunCoreStreamingAsync(messages, session, options, cancellationToken)
            .ToAgentResponseAsync(cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messagesList = messages.ToList();
        string? userText = messagesList.LastOrDefault(m => m.Role == ChatRole.User)?.Text?.ToUpperInvariant();
        var lastMessage = messagesList.LastOrDefault(m => m.Role == ChatRole.User);

        // Check if this is a resume from a previous interrupt
        var approvalResponse = lastMessage?.Contents.OfType<FunctionApprovalResponseContent>().FirstOrDefault();
        if (approvalResponse is not null)
        {
            string messageId = Guid.NewGuid().ToString("N");

            if (approvalResponse.Approved)
            {
                yield return new AgentResponseUpdate
                {
                    MessageId = messageId,
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent($"✅ Function '{approvalResponse.FunctionCall.Name}' was approved. Executing...")]
                };

                // Simulate execution
                await Task.Delay(100, cancellationToken);

                yield return new AgentResponseUpdate
                {
                    MessageId = messageId,
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent("\n\n🎉 Operation completed successfully!")]
                };
            }
            else
            {
                yield return new AgentResponseUpdate
                {
                    MessageId = messageId,
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent($"❌ Function '{approvalResponse.FunctionCall.Name}' was rejected. Operation cancelled.")]
                };
            }

            yield break;
        }

        // Normal flow - check for trigger phrases
        if (userText?.Contains("DELETE", StringComparison.Ordinal) == true)
        {
            // Request approval for delete operation
            string approvalId = $"approval_{Guid.NewGuid():N}";
            const string filename = "/etc/important.conf"; // Simulated filename

            yield return new AgentResponseUpdate
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Role = ChatRole.Assistant,
                Contents = [new TextContent("I'll help you delete that file. However, this is a sensitive operation that requires your approval.")]
            };

            // Emit FunctionApprovalRequestContent - framework will convert to interrupt
            var functionCall = new FunctionCallContent(approvalId, "delete_file", new Dictionary<string, object?> { ["filename"] = filename });
            var approvalRequest = new FunctionApprovalRequestContent(approvalId, functionCall);

            yield return new AgentResponseUpdate
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Role = ChatRole.Assistant,
                Contents = [approvalRequest]
            };
        }
        else if (userText?.Contains("TRANSFER", StringComparison.Ordinal) == true)
        {
            // Request approval for transfer operation
            string approvalId = $"approval_{Guid.NewGuid():N}";

            yield return new AgentResponseUpdate
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Role = ChatRole.Assistant,
                Contents = [new TextContent("I'll initiate the money transfer. This requires your approval before proceeding.")]
            };

            var functionCall = new FunctionCallContent(
                approvalId,
                "transfer_money",
                new Dictionary<string, object?>
                {
                    ["fromAccount"] = "checking-1234",
                    ["toAccount"] = "savings-5678",
                    ["amount"] = 500.00m
                });
            var approvalRequest = new FunctionApprovalRequestContent(approvalId, functionCall);

            yield return new AgentResponseUpdate
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Role = ChatRole.Assistant,
                Contents = [approvalRequest]
            };
        }
        else
        {
            // Normal response
            string messageId = Guid.NewGuid().ToString("N");
            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextContent("Hello! I'm a sample agent that demonstrates function approval via AG-UI interrupts.\n\n")]
            };

            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextContent("Try saying:\n")]
            };

            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextContent("- \"delete the file\" - to trigger a file deletion approval\n")]
            };

            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextContent("- \"transfer money\" - to trigger a money transfer approval")]
            };
        }

        await Task.CompletedTask;
    }

    public override ValueTask<AgentSession> GetNewSessionAsync(CancellationToken cancellationToken = default) =>
        new(new SampleAgentSession());

    public override ValueTask<AgentSession> DeserializeSessionAsync(
        JsonElement serializedSession,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) =>
        new(new SampleAgentSession(serializedSession, jsonSerializerOptions));

    public override object? GetService(Type serviceType, object? serviceKey = null) => null;

    private sealed class SampleAgentSession : InMemoryAgentSession
    {
        public SampleAgentSession() : base() { }
        public SampleAgentSession(JsonElement serializedSession, JsonSerializerOptions? jsonSerializerOptions = null)
            : base(serializedSession, jsonSerializerOptions) { }
    }
}
