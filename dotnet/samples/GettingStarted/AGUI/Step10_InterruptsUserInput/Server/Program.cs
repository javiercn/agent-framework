// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using UserInputServer;

#pragma warning disable MEAI001 // Experimental API - UserInputRequestContent

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient().AddLogging();
builder.Services.AddAGUI();

WebApplication app = builder.Build();

// Create a custom agent that requests user input via interrupts
var agent = new UserInputRequestAgent();

// Map the AG-UI endpoint - interrupts are handled automatically by the framework
app.MapAGUI("/", agent);

await app.RunAsync();

namespace UserInputServer
{
    /// <summary>
    /// A sample agent that demonstrates requesting user input via AG-UI interrupts.
    /// When the agent needs additional information, it emits a UserInputRequestContent
    /// which the framework converts to a RUN_FINISHED event with outcome="interrupt".
    /// </summary>
    internal sealed class UserInputRequestAgent : AIAgent
    {
        public override string? Description => "Agent that requests user input via interrupts";

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
            var userInputResponse = lastMessage?.Contents.OfType<UserInputResponseContent>().FirstOrDefault();
            if (userInputResponse is not null)
            {
                // User provided the requested input
                string messageId = Guid.NewGuid().ToString("N");

                // Extract the response from RawRepresentation
                string? providedInput = null;
                if (userInputResponse.RawRepresentation is JsonElement json)
                {
                    if (json.TryGetProperty("response", out var responseElement))
                    {
                        providedInput = responseElement.GetString();
                    }
                    else
                    {
                        providedInput = json.GetRawText();
                    }
                }

                yield return new AgentResponseUpdate
                {
                    MessageId = messageId,
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent($"Thank you! I received your input: \"{providedInput}\". Processing your request...")]
                };

                // Simulate some processing
                await Task.Delay(100, cancellationToken);

                yield return new AgentResponseUpdate
                {
                    MessageId = messageId,
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent("\n\nYour request has been processed successfully!")]
                };

                yield break;
            }

            // Normal flow - check for trigger phrases
            if (userText?.Contains("SETUP", StringComparison.Ordinal) == true || userText?.Contains("CONFIGURE", StringComparison.Ordinal) == true)
            {
                // Request API key via interrupt
                string inputRequestId = $"input_{Guid.NewGuid():N}";

                yield return new AgentResponseUpdate
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent("I'll help you set up your account. First, I need some information from you.")]
                };

                // Emit UserInputRequestContent - framework will convert to interrupt
                var inputRequest = new TestUserInputRequestContent(inputRequestId)
                {
                    RawRepresentation = JsonDocument.Parse("""
                        {
                            "prompt": "Please enter your API key",
                            "inputType": "password",
                            "required": true
                        }
                        """).RootElement
                };

                yield return new AgentResponseUpdate
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    Role = ChatRole.Assistant,
                    Contents = [inputRequest]
                };
            }
            else if (userText?.Contains("EMAIL", StringComparison.Ordinal) == true || userText?.Contains("CONTACT", StringComparison.Ordinal) == true)
            {
                // Request email via interrupt
                string inputRequestId = $"input_{Guid.NewGuid():N}";

                yield return new AgentResponseUpdate
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent("I can help you with that. I just need to verify your contact information.")]
                };

                var inputRequest = new TestUserInputRequestContent(inputRequestId)
                {
                    RawRepresentation = JsonDocument.Parse("""
                        {
                            "prompt": "Please enter your email address",
                            "inputType": "email",
                            "required": true
                        }
                        """).RootElement
                };

                yield return new AgentResponseUpdate
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    Role = ChatRole.Assistant,
                    Contents = [inputRequest]
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
                    Contents = [new TextContent("Hello! I'm a sample agent that demonstrates user input interrupts. Try saying:\n")]
                };

                yield return new AgentResponseUpdate
                {
                    MessageId = messageId,
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent("- \"setup my account\" - to trigger an API key input request\n")]
                };

                yield return new AgentResponseUpdate
                {
                    MessageId = messageId,
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent("- \"update my email\" - to trigger an email input request")]
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

    /// <summary>
    /// Test implementation of UserInputRequestContent that can be instantiated directly.
    /// </summary>
    internal sealed class TestUserInputRequestContent : UserInputRequestContent
    {
        public TestUserInputRequestContent(string id) : base(id)
        {
        }
    }
}
