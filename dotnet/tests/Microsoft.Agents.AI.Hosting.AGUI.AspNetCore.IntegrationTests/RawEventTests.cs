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
using AGUI.Protocol;
using FluentAssertions;
using Microsoft.Agents.AI.AGUI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests;

/// <summary>
/// Integration tests for raw AG-UI event emission via RawRepresentation.
/// When raw AG-UI events are emitted by an agent and processed through AGUIChatClient,
/// the AG-UI event is stored in ChatResponseUpdate.RawRepresentation, which is then
/// wrapped by AgentResponseUpdate (whose RawRepresentation points to the ChatResponseUpdate).
/// </summary>
public sealed class RawEventTests : IAsyncDisposable
{
    private WebApplication? _app;
    private HttpClient? _client;

    /// <summary>
    /// Recursively extracts an AG-UI BaseEvent from an AgentResponseUpdate by walking through
    /// the chain of RawRepresentation properties. This handles any level of nesting where
    /// the event may be wrapped in AgentResponseUpdate -> ChatResponseUpdate -> ... -> BaseEvent.
    /// </summary>
    private static T? ToAGUIEvent<T>(AgentResponseUpdate update) where T : BaseEvent
    {
        return FindBaseEvent<T>(update.RawRepresentation);
    }

    /// <summary>
    /// Recursively searches for a BaseEvent of type T in the RawRepresentation chain.
    /// </summary>
    private static T? FindBaseEvent<T>(object? obj) where T : BaseEvent
    {
        return obj switch
        {
            T baseEvent => baseEvent,
            AgentResponseUpdate aru => FindBaseEvent<T>(aru.RawRepresentation),
            ChatResponseUpdate cru => FindBaseEvent<T>(cru.RawRepresentation),
            _ => null
        };
    }

    [Fact]
    public async Task AgentEmittingStateSnapshotEventViaRawRepresentation_IsReceivedByClient_WithRawEventAsync()
    {
        // Arrange
        var fakeAgent = new FakeRawEventAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "emit state snapshot");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();

        // Verify that the AG-UI event is accessible via ChatResponseUpdate.RawRepresentation
        AgentResponseUpdate? stateUpdate = updates.FirstOrDefault(u => ToAGUIEvent<StateSnapshotEvent>(u) is not null);
        stateUpdate.Should().NotBeNull("should receive update with StateSnapshotEvent in ChatResponseUpdate.RawRepresentation");

        StateSnapshotEvent stateSnapshot = ToAGUIEvent<StateSnapshotEvent>(stateUpdate!)!;
        stateSnapshot.Snapshot.Should().NotBeNull();
        stateSnapshot.Snapshot!.Value.GetProperty("counter").GetInt32().Should().Be(42);
        stateSnapshot.Snapshot!.Value.GetProperty("status").GetString().Should().Be("active");
    }

    [Fact]
    public async Task AgentEmittingStateDeltaEventViaRawRepresentation_IsReceivedByClient_WithRawEventAsync()
    {
        // Arrange
        var fakeAgent = new FakeRawEventAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "emit state delta");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();

        // Verify that the AG-UI event is accessible via ChatResponseUpdate.RawRepresentation
        AgentResponseUpdate? deltaUpdate = updates.FirstOrDefault(u => ToAGUIEvent<StateDeltaEvent>(u) is not null);
        deltaUpdate.Should().NotBeNull("should receive update with StateDeltaEvent in ChatResponseUpdate.RawRepresentation");

        StateDeltaEvent stateDelta = ToAGUIEvent<StateDeltaEvent>(deltaUpdate!)!;
        stateDelta.Delta.Should().NotBeNull();
        stateDelta.Delta!.Value.GetArrayLength().Should().Be(1);
        stateDelta.Delta!.Value[0].GetProperty("op").GetString().Should().Be("replace");
        stateDelta.Delta!.Value[0].GetProperty("path").GetString().Should().Be("/counter");
        stateDelta.Delta!.Value[0].GetProperty("value").GetInt32().Should().Be(43);
    }

    [Fact]
    public async Task AgentEmittingCustomEventViaRawRepresentation_IsReceivedByClient_WithRawEventAsync()
    {
        // Arrange
        var fakeAgent = new FakeRawEventAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "emit custom event");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();

        // Verify that the AG-UI event is accessible via ChatResponseUpdate.RawRepresentation
        AgentResponseUpdate? customUpdate = updates.FirstOrDefault(u => ToAGUIEvent<CustomEvent>(u) is not null);
        customUpdate.Should().NotBeNull("should receive update with CustomEvent in ChatResponseUpdate.RawRepresentation");

        CustomEvent customEvent = ToAGUIEvent<CustomEvent>(customUpdate!)!;
        customEvent.Name.Should().Be("test_custom_event");
        customEvent.Value.GetProperty("foo").GetString().Should().Be("bar");
    }

    [Fact]
    public async Task MixedRawAndNormalUpdates_AreReceivedCorrectlyAsync()
    {
        // Arrange
        var fakeAgent = new FakeRawEventAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "emit mixed");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();

        // Should have RunStartedEvent (via ChatResponseUpdate.RawRepresentation)
        updates.Count(u => ToAGUIEvent<RunStartedEvent>(u) is not null).Should().BeGreaterThan(0, "should have RunStartedEvent");

        // Should have StateSnapshotEvent (via ChatResponseUpdate.RawRepresentation)
        updates.Count(u => ToAGUIEvent<StateSnapshotEvent>(u) is not null).Should().BeGreaterThan(0, "should have StateSnapshotEvent");

        // Should have text content (via ChatResponseUpdate.RawRepresentation)
        updates.Count(u => ToAGUIEvent<TextMessageContentEvent>(u) is not null).Should().BeGreaterThan(0, "should have TextMessageContentEvent");

        // Should have RunFinishedEvent (via ChatResponseUpdate.RawRepresentation)
        updates.Count(u => ToAGUIEvent<RunFinishedEvent>(u) is not null).Should().BeGreaterThan(0, "should have RunFinishedEvent");
    }

    [Fact]
    public async Task AllLifecycleEvents_HaveRawRepresentationSetAsync()
    {
        // Arrange
        var fakeAgent = new FakeRawEventAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "hello");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert - every update should have RawRepresentation set, and it should be a ChatResponseUpdate
        // with its own RawRepresentation set to the AG-UI event
        foreach (AgentResponseUpdate update in updates)
        {
            update.RawRepresentation.Should().NotBeNull("all updates should have RawRepresentation set");
            update.RawRepresentation.Should().BeOfType<ChatResponseUpdate>("AgentResponseUpdate.RawRepresentation should be the ChatResponseUpdate");

            var chatUpdate = (ChatResponseUpdate)update.RawRepresentation!;
            chatUpdate.RawRepresentation.Should().NotBeNull("ChatResponseUpdate.RawRepresentation should be set to the AG-UI event");
            chatUpdate.RawRepresentation.Should().BeAssignableTo<BaseEvent>("ChatResponseUpdate.RawRepresentation should be an AG-UI event");
        }
    }

    [Fact]
    public async Task AgentExplicitlyEmittingLifecycleEvents_DoesNotDuplicateAsync()
    {
        // Arrange
        var fakeAgent = new FakeRawEventAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "emit lifecycle events");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();

        // Should have exactly ONE RunStartedEvent (the explicit one)
        int runStartedCount = updates.Count(u => ToAGUIEvent<RunStartedEvent>(u) is not null);
        runStartedCount.Should().Be(1, "RunStartedEvent should not be duplicated when explicitly emitted");

        // Verify the explicit RunStartedEvent has custom thread/run IDs
        var runStarted = ToAGUIEvent<RunStartedEvent>(updates.First(u => ToAGUIEvent<RunStartedEvent>(u) is not null))!;
        runStarted.ThreadId.Should().Be("custom-thread-123");
        runStarted.RunId.Should().Be("custom-run-456");

        // Should have exactly ONE RunFinishedEvent (the explicit one)
        int runFinishedCount = updates.Count(u => ToAGUIEvent<RunFinishedEvent>(u) is not null);
        runFinishedCount.Should().Be(1, "RunFinishedEvent should not be duplicated when explicitly emitted");

        // Verify the explicit RunFinishedEvent has custom thread/run IDs
        var runFinished = ToAGUIEvent<RunFinishedEvent>(updates.First(u => ToAGUIEvent<RunFinishedEvent>(u) is not null))!;
        runFinished.ThreadId.Should().Be("custom-thread-123");
        runFinished.RunId.Should().Be("custom-run-456");
    }

    [Fact]
    public async Task AgentEmittingTextReasoningContent_IsConvertedToReasoningEvents_AndBackToTextReasoningContentAsync()
    {
        // Arrange
        var fakeAgent = new FakeRawEventAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "emit reasoning content");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();

        // Should have ReasoningStartEvent
        updates.Count(u => ToAGUIEvent<ReasoningStartEvent>(u) is not null).Should().BeGreaterThan(0, "should have ReasoningStartEvent");

        // Should have ReasoningMessageStartEvent
        updates.Count(u => ToAGUIEvent<ReasoningMessageStartEvent>(u) is not null).Should().BeGreaterThan(0, "should have ReasoningMessageStartEvent");

        // Should have ReasoningMessageContentEvent with reasoning text
        var reasoningContentUpdates = updates.Where(u => ToAGUIEvent<ReasoningMessageContentEvent>(u) is not null).ToList();
        reasoningContentUpdates.Should().NotBeEmpty("should have ReasoningMessageContentEvent");

        // Verify the client receives TextReasoningContent
        var textReasoningUpdates = updates.Where(u => u.Contents.OfType<TextReasoningContent>().Any()).ToList();
        textReasoningUpdates.Should().NotBeEmpty("client should receive TextReasoningContent");

        // Verify the reasoning text content
        string combinedReasoningText = string.Concat(textReasoningUpdates.SelectMany(u => u.Contents.OfType<TextReasoningContent>().Select(t => t.Text)));
        combinedReasoningText.Should().Contain("Let me think", "reasoning text should contain expected content");

        // Should have ReasoningMessageEndEvent
        updates.Count(u => ToAGUIEvent<ReasoningMessageEndEvent>(u) is not null).Should().BeGreaterThan(0, "should have ReasoningMessageEndEvent");

        // Should have ReasoningEndEvent
        updates.Count(u => ToAGUIEvent<ReasoningEndEvent>(u) is not null).Should().BeGreaterThan(0, "should have ReasoningEndEvent");

        // Should also have regular text content after reasoning
        var textContentUpdates = updates.Where(u => u.Contents.OfType<TextContent>().Any()).ToList();
        textContentUpdates.Should().NotBeEmpty("should have regular TextContent after reasoning");
    }

    private async Task SetupTestServerAsync(FakeRawEventAgent fakeAgent)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddAGUI();
        builder.WebHost.UseTestServer();

        this._app = builder.Build();

        this._app.MapAGUI("/agent", fakeAgent);

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
/// Fake agent that emits AG-UI events directly via RawRepresentation.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated in tests")]
internal sealed class FakeRawEventAgent : AIAgent
{
    public override string? Description => "Agent for raw event testing";

    protected override Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this.RunCoreStreamingAsync(messages, session, options, cancellationToken).ToAgentResponseAsync(cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? userText = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text;

        if (userText?.Contains("emit state snapshot", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Emit StateSnapshotEvent directly via RawRepresentation
            var stateSnapshotEvent = new StateSnapshotEvent
            {
                Snapshot = JsonSerializer.SerializeToElement(new { counter = 42, status = "active" })
            };

            yield return new AgentResponseUpdate
            {
                RawRepresentation = stateSnapshotEvent,
                Contents = []
            };
        }
        else if (userText?.Contains("emit state delta", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Emit StateDeltaEvent directly via RawRepresentation
            var stateDeltaEvent = new StateDeltaEvent
            {
                Delta = JsonSerializer.SerializeToElement(new[] { new { op = "replace", path = "/counter", value = 43 } })
            };

            yield return new AgentResponseUpdate
            {
                RawRepresentation = stateDeltaEvent,
                Contents = []
            };
        }
        else if (userText?.Contains("emit custom event", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Emit CustomEvent directly via RawRepresentation
            var customEvent = new CustomEvent
            {
                Name = "test_custom_event",
                Value = JsonSerializer.SerializeToElement(new { foo = "bar" })
            };

            yield return new AgentResponseUpdate
            {
                RawRepresentation = customEvent,
                Contents = []
            };
        }
        else if (userText?.Contains("emit mixed", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Emit a StateSnapshotEvent via RawRepresentation
            var stateSnapshotEvent = new StateSnapshotEvent
            {
                Snapshot = JsonSerializer.SerializeToElement(new { counter = 100 })
            };

            yield return new AgentResponseUpdate
            {
                RawRepresentation = stateSnapshotEvent,
                Contents = []
            };

            // Also emit a normal text update
            yield return new AgentResponseUpdate
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Role = ChatRole.Assistant,
                Contents = [new TextContent("Mixed response with state")]
            };
        }
        else if (userText?.Contains("emit lifecycle events", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Explicitly emit RunStartedEvent with custom IDs
            yield return new AgentResponseUpdate
            {
                RawRepresentation = new RunStartedEvent
                {
                    ThreadId = "custom-thread-123",
                    RunId = "custom-run-456"
                },
                Contents = []
            };

            // Emit some text
            yield return new AgentResponseUpdate
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Role = ChatRole.Assistant,
                Contents = [new TextContent("Response with explicit lifecycle")]
            };

            // Explicitly emit RunFinishedEvent with custom IDs
            yield return new AgentResponseUpdate
            {
                RawRepresentation = new RunFinishedEvent
                {
                    ThreadId = "custom-thread-123",
                    RunId = "custom-run-456"
                },
                Contents = []
            };
        }
        else if (userText?.Contains("emit reasoning content", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Emit TextReasoningContent which should be converted to REASONING_* events by the server
            // and then back to TextReasoningContent on the client side
            string messageId = Guid.NewGuid().ToString("N");

            // Emit reasoning content (like a thinking model would)
            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextReasoningContent("Let me think about this problem. ")]
            };

            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextReasoningContent("The user wants to test reasoning events. ")]
            };

            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextReasoningContent("I should provide a thoughtful response.")]
            };

            // Now emit regular text content (the actual response after thinking)
            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextContent("Here is my response after reasoning.")]
            };
        }
        else
        {
            // Default: just return text
            string messageId = Guid.NewGuid().ToString("N");
            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextContent("Hello from raw event agent")]
            };
        }

        await Task.CompletedTask;
    }

    public override ValueTask<AgentSession> GetNewSessionAsync(CancellationToken cancellationToken = default) =>
        new(new FakeInMemoryAgentSession());

    public override ValueTask<AgentSession> DeserializeSessionAsync(JsonElement serializedSession, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default) =>
        new(new FakeInMemoryAgentSession(serializedSession, jsonSerializerOptions));

    private sealed class FakeInMemoryAgentSession : InMemoryAgentSession
    {
        public FakeInMemoryAgentSession()
            : base()
        {
        }

        public FakeInMemoryAgentSession(JsonElement serializedSession, JsonSerializerOptions? jsonSerializerOptions = null)
            : base(serializedSession, jsonSerializerOptions)
        {
        }
    }

    public override object? GetService(Type serviceType, object? serviceKey = null) => null;
}
