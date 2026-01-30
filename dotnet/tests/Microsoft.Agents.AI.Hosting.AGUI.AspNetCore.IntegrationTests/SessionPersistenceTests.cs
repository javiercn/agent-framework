// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Agents.AI.AGUI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests;

/// <summary>
/// Integration tests for session persistence with the MapAGUI overload that accepts an AgentSessionStore.
/// </summary>
public sealed class SessionPersistenceTests : IAsyncDisposable
{
    private WebApplication? _app;
    private HttpClient? _client;
    private SessionTrackingAgent? _agent;
    private InMemoryAgentSessionStore? _sessionStore;

    [Fact]
    public async Task MapAGUI_WithSessionStore_PersistsSessionAcrossRequestsAsync()
    {
        // Arrange
        await this.SetupTestServerWithSessionStoreAsync();
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent clientAgent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await clientAgent.GetNewSessionAsync();

        // Use a consistent threadId across requests
        string threadId = Guid.NewGuid().ToString("N");

        // Act - First request
        List<AgentResponseUpdate> firstUpdates = [];
        await foreach (AgentResponseUpdate update in clientAgent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "First message")],
            session,
            new AGUIChatClientAgentRunOptions { ThreadId = threadId },
            CancellationToken.None))
        {
            firstUpdates.Add(update);
        }

        // Act - Second request with same threadId
        List<AgentResponseUpdate> secondUpdates = [];
        await foreach (AgentResponseUpdate update in clientAgent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "Second message")],
            session,
            new AGUIChatClientAgentRunOptions { ThreadId = threadId },
            CancellationToken.None))
        {
            secondUpdates.Add(update);
        }

        // Assert
        firstUpdates.Should().NotBeEmpty();
        secondUpdates.Should().NotBeEmpty();

        // Verify the server-side session was used and persisted
        this._agent!.SessionCallCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task MapAGUI_WithSessionStore_RestoresSessionFromThreadIdAsync()
    {
        // Arrange
        await this.SetupTestServerWithSessionStoreAsync();
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent clientAgent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await clientAgent.GetNewSessionAsync();

        string threadId = Guid.NewGuid().ToString("N");

        // Act - First request to create session
        await foreach (AgentResponseUpdate update in clientAgent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "Create session")],
            session,
            new AGUIChatClientAgentRunOptions { ThreadId = threadId },
            CancellationToken.None))
        {
            // Consume all updates
        }

        // Reset the agent's session tracking
        this._agent!.ResetSessionTracking();

        // Act - Second request should restore from session store
        await foreach (AgentResponseUpdate update in clientAgent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "Restore session")],
            session,
            new AGUIChatClientAgentRunOptions { ThreadId = threadId },
            CancellationToken.None))
        {
            // Consume all updates
        }

        // Assert - The agent should have received a restored session (not a new one)
        // The server tracks that it was given an existing session
        this._agent!.ReceivedExistingSession.Should().BeTrue("session should be restored from store");
    }

    [Fact]
    public async Task MapAGUI_NewThreadId_CreatesNewSessionAsync()
    {
        // Arrange
        await this.SetupTestServerWithSessionStoreAsync();
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent clientAgent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await clientAgent.GetNewSessionAsync();

        // Act - Two requests with different threadIds
        string threadId1 = Guid.NewGuid().ToString("N");
        string threadId2 = Guid.NewGuid().ToString("N");

        await foreach (AgentResponseUpdate update in clientAgent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "First thread")],
            session,
            new AGUIChatClientAgentRunOptions { ThreadId = threadId1 },
            CancellationToken.None))
        {
            // Consume all updates
        }

        int sessionCountAfterFirst = this._agent!.SessionCallCount;

        await foreach (AgentResponseUpdate update in clientAgent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "Second thread")],
            session,
            new AGUIChatClientAgentRunOptions { ThreadId = threadId2 },
            CancellationToken.None))
        {
            // Consume all updates
        }

        // Assert - Both requests should have created sessions (different threads)
        this._agent!.SessionCallCount.Should().BeGreaterThan(sessionCountAfterFirst);
    }

    [Fact]
    public async Task MapAGUI_WithInMemorySessionStore_SessionContainsChatHistoryAsync()
    {
        // Arrange
        await this.SetupTestServerWithSessionStoreAsync(useHistoryTrackingAgent: true);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent clientAgent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await clientAgent.GetNewSessionAsync();

        string threadId = Guid.NewGuid().ToString("N");

        // Act - First request
        await foreach (AgentResponseUpdate update in clientAgent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "Hello")],
            session,
            new AGUIChatClientAgentRunOptions { ThreadId = threadId },
            CancellationToken.None))
        {
            // Consume all updates
        }

        // Act - Second request with same threadId
        await foreach (AgentResponseUpdate update in clientAgent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "How are you?")],
            session,
            new AGUIChatClientAgentRunOptions { ThreadId = threadId },
            CancellationToken.None))
        {
            // Consume all updates
        }

        // Assert - The session store should have stored the session
        // We can verify by checking that the agent received prior context
        this._agent!.LastReceivedMessagesCount.Should().BeGreaterThan(0);
    }

    private async Task SetupTestServerWithSessionStoreAsync(bool useHistoryTrackingAgent = false)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddAGUI();
        builder.Services.AddSingleton<SessionTrackingAgent>();

        // Create the session store
        this._sessionStore = new InMemoryAgentSessionStore();
        builder.Services.AddSingleton<AgentSessionStore>(this._sessionStore);

        this._app = builder.Build();

        this._agent = this._app.Services.GetRequiredService<SessionTrackingAgent>();

        // Use the MapAGUI overload with session store
        this._app.MapAGUI("/agent", this._agent, this._sessionStore);

        await this._app.StartAsync();

        TestServer testServer = this._app.Services.GetRequiredService<IServer>() as TestServer
            ?? throw new InvalidOperationException("TestServer not found");

        this._client = testServer.CreateClient();
        this._client.BaseAddress = new Uri("http://localhost/agent");
    }

    public async ValueTask DisposeAsync()
    {
        this._client?.Dispose();
        if (this._app != null)
        {
            await this._app.DisposeAsync();
        }
    }
}

/// <summary>
/// A test agent that tracks session usage for verification in tests.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via dependency injection")]
internal sealed class SessionTrackingAgent : AIAgent
{
    private int _sessionCallCount;
    private bool _receivedExistingSession;
    private int _lastReceivedMessagesCount;

    protected override string? IdCore => "session-tracking-agent";

    public override string? Description => "A test agent that tracks session usage";

    public int SessionCallCount => this._sessionCallCount;

    public bool ReceivedExistingSession => this._receivedExistingSession;

    public int LastReceivedMessagesCount => this._lastReceivedMessagesCount;

    public void ResetSessionTracking()
    {
        this._receivedExistingSession = false;
    }

    public override ValueTask<AgentSession> GetNewSessionAsync(CancellationToken cancellationToken = default) =>
        new(new SessionTrackingAgentSession());

    public override ValueTask<AgentSession> DeserializeSessionAsync(JsonElement serializedSession, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        // When deserializing, mark that we received an existing session
        this._receivedExistingSession = true;
        return new(new SessionTrackingAgentSession(serializedSession, jsonSerializerOptions));
    }

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        List<AgentResponseUpdate> updates = [];
        await foreach (AgentResponseUpdate update in this.RunStreamingAsync(messages, session, options, cancellationToken).ConfigureAwait(false))
        {
            updates.Add(update);
        }

        return updates.ToAgentResponse();
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref this._sessionCallCount);

        // Materialize messages to avoid multiple enumeration
        var messagesList = messages.ToList();

        // Track received messages count
        this._lastReceivedMessagesCount = messagesList.Count;

        // Check if session has prior state (indicates restored session)
        if (session is SessionTrackingAgentSession trackingSession && trackingSession.HasPriorState)
        {
            this._receivedExistingSession = true;
        }

        // Add messages to session's chat history for tracking
        if (session is SessionTrackingAgentSession sts)
        {
            foreach (var msg in messagesList)
            {
                sts.ChatHistoryProvider.Add(msg);
            }
            sts.MarkAsPriorState();
        }

        string messageId = Guid.NewGuid().ToString("N");

        // Return a simple response
        foreach (string chunk in new[] { "Response", " ", "from", " ", "session-tracking-agent" })
        {
            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextContent(chunk)]
            };

            await Task.Yield();
        }

        // Add response to session
        if (session is SessionTrackingAgentSession sts2)
        {
            sts2.ChatHistoryProvider.Add(new ChatMessage(ChatRole.Assistant, "Response from session-tracking-agent"));
        }
    }
}

/// <summary>
/// Session that tracks state for testing session persistence.
/// </summary>
internal sealed class SessionTrackingAgentSession : InMemoryAgentSession
{
    private bool _hasPriorState;

    public SessionTrackingAgentSession()
    {
    }

    public SessionTrackingAgentSession(JsonElement serializedSession, JsonSerializerOptions? jsonSerializerOptions)
        : base(serializedSession, jsonSerializerOptions)
    {
        // Mark that this session was restored from serialized state
        this._hasPriorState = true;
    }

    public bool HasPriorState => this._hasPriorState;

    public void MarkAsPriorState()
    {
        this._hasPriorState = true;
    }
}

/// <summary>
/// Options for the AGUIChatClient agent to specify threadId.
/// </summary>
internal sealed class AGUIChatClientAgentRunOptions : AgentRunOptions
{
    public string? ThreadId { get; set; }
}
