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

#pragma warning disable MEAI001 // Experimental API - FunctionApprovalRequestContent, UserInputRequestContent

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests;

/// <summary>
/// Integration tests for AG-UI interrupt/resume functionality.
/// Tests the complete interrupt lifecycle from server to client and back.
/// </summary>
public sealed class InterruptTests : IAsyncDisposable
{
    private WebApplication? _app;
    private HttpClient? _client;

    /// <summary>
    /// Recursively extracts an AG-UI BaseEvent from an AgentResponseUpdate.
    /// </summary>
    private static T? ToAGUIEvent<T>(AgentResponseUpdate update) where T : BaseEvent
    {
        return FindBaseEvent<T>(update.RawRepresentation);
    }

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
    public async Task Agent_WithFunctionApprovalRequest_EmitsInterruptEvent_WithApprovalIdAsInterruptIdAsync()
    {
        // Arrange
        var fakeAgent = new FakeInterruptAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "trigger function approval");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();

        // Find the RunFinishedEvent with interrupt
        var runFinishedUpdate = updates.FirstOrDefault(u => ToAGUIEvent<RunFinishedEvent>(u) is { Outcome: "interrupt" });
        runFinishedUpdate.Should().NotBeNull("should receive RunFinishedEvent with interrupt outcome");

        var runFinished = ToAGUIEvent<RunFinishedEvent>(runFinishedUpdate!)!;
        runFinished.Outcome.Should().Be("interrupt");
        runFinished.Interrupt.Should().NotBeNull();
        runFinished.Interrupt!.Id.Should().Be("approval_123", "interrupt id should match the FunctionApprovalRequestContent.Id");
    }

    [Fact]
    public async Task Agent_WithFunctionApprovalRequest_EmitsInterruptEvent_WithFunctionDetailsInPayloadAsync()
    {
        // Arrange
        var fakeAgent = new FakeInterruptAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "trigger function approval");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        var runFinishedUpdate = updates.FirstOrDefault(u => ToAGUIEvent<RunFinishedEvent>(u) is { Outcome: "interrupt" });
        runFinishedUpdate.Should().NotBeNull();

        var runFinished = ToAGUIEvent<RunFinishedEvent>(runFinishedUpdate!)!;
        runFinished.Interrupt.Should().NotBeNull();
        runFinished.Interrupt!.Payload.Should().NotBeNull();

        // Verify payload contains function details
        var payload = runFinished.Interrupt.Payload!.Value;
        payload.GetProperty("functionName").GetString().Should().Be("delete_important_file");
        payload.GetProperty("functionArguments").GetProperty("filename").GetString().Should().Be("/etc/important.conf");
    }

    [Fact]
    public async Task Agent_WithUserInputRequest_EmitsInterruptEvent_WithRawRepresentationAsync()
    {
        // Arrange
        var fakeAgent = new FakeInterruptAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "trigger user input request");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();

        // Find the RunFinishedEvent with interrupt
        var runFinishedUpdate = updates.FirstOrDefault(u => ToAGUIEvent<RunFinishedEvent>(u) is { Outcome: "interrupt" });
        runFinishedUpdate.Should().NotBeNull("should receive RunFinishedEvent with interrupt outcome");

        var runFinished = ToAGUIEvent<RunFinishedEvent>(runFinishedUpdate!)!;
        runFinished.Outcome.Should().Be("interrupt");
        runFinished.Interrupt.Should().NotBeNull();
        runFinished.Interrupt!.Id.Should().Be("input_456", "interrupt id should match the UserInputRequestContent.Id");
    }

    [Fact]
    public async Task Client_ReceivesFunctionApprovalRequestContent_FromInterruptEventAsync()
    {
        // Arrange
        var fakeAgent = new FakeInterruptAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "trigger function approval");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert - client should receive FunctionApprovalRequestContent
        var approvalContent = updates
            .SelectMany(u => u.Contents)
            .OfType<FunctionApprovalRequestContent>()
            .FirstOrDefault();

        approvalContent.Should().NotBeNull("client should receive FunctionApprovalRequestContent");
        approvalContent!.Id.Should().Be("approval_123");
        approvalContent.FunctionCall.Name.Should().Be("delete_important_file");
    }

    [Fact]
    public async Task Client_ReceivesUserInputRequestContent_FromInterruptEventAsync()
    {
        // Arrange
        var fakeAgent = new FakeInterruptAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "trigger user input request");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert - client should receive UserInputRequestContent
        var inputContent = updates
            .SelectMany(u => u.Contents)
            .OfType<UserInputRequestContent>()
            .FirstOrDefault(u => u is not FunctionApprovalRequestContent); // Exclude function approval

        inputContent.Should().NotBeNull("client should receive UserInputRequestContent");
        inputContent!.Id.Should().Be("input_456");
    }

    [Fact]
    public async Task InterruptId_PreservedAcrossRoundTrip_ForFunctionApprovalAsync()
    {
        // Arrange
        var fakeAgent = new FakeInterruptAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "trigger function approval");

        List<AgentResponseUpdate> updates = [];

        // Act - First request triggers interrupt
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Get the interrupt
        var runFinished = ToAGUIEvent<RunFinishedEvent>(updates.First(u => ToAGUIEvent<RunFinishedEvent>(u) is { Outcome: "interrupt" }))!;

        // Assert - ID is preserved
        runFinished.Interrupt!.Id.Should().Be("approval_123");

        // The client would then parse this interrupt and create FunctionApprovalRequestContent with same ID
        var approvalRequest = updates
            .SelectMany(u => u.Contents)
            .OfType<FunctionApprovalRequestContent>()
            .First();

        approvalRequest.Id.Should().Be("approval_123", "FunctionApprovalRequestContent.Id should match interrupt ID");
    }

    [Fact]
    public async Task InterruptId_PreservedAcrossRoundTrip_ForUserInputAsync()
    {
        // Arrange
        var fakeAgent = new FakeInterruptAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "trigger user input request");

        List<AgentResponseUpdate> updates = [];

        // Act - First request triggers interrupt
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Get the interrupt
        var runFinished = ToAGUIEvent<RunFinishedEvent>(updates.First(u => ToAGUIEvent<RunFinishedEvent>(u) is { Outcome: "interrupt" }))!;

        // Assert - ID is preserved
        runFinished.Interrupt!.Id.Should().Be("input_456");

        // The client would then parse this interrupt and create UserInputRequestContent with same ID
        var inputRequest = updates
            .SelectMany(u => u.Contents)
            .OfType<UserInputRequestContent>()
            .First(u => u is not FunctionApprovalRequestContent);

        inputRequest.Id.Should().Be("input_456", "UserInputRequestContent.Id should match interrupt ID");
    }

    [Fact]
    public async Task RunFinishedEvent_WithSuccess_DoesNotHaveInterruptAsync()
    {
        // Arrange
        var fakeAgent = new FakeInterruptAgent();

        await this.SetupTestServerAsync(fakeAgent);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        ChatMessage userMessage = new(ChatRole.User, "hello"); // Normal message, no interrupt

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        var runFinishedUpdate = updates.FirstOrDefault(u => ToAGUIEvent<RunFinishedEvent>(u) is not null);
        runFinishedUpdate.Should().NotBeNull("should receive RunFinishedEvent");

        var runFinished = ToAGUIEvent<RunFinishedEvent>(runFinishedUpdate!)!;

        // For successful completion, either outcome is null (back-compat) or "success"
        if (runFinished.Outcome is not null)
        {
            runFinished.Outcome.Should().Be("success");
        }

        runFinished.Interrupt.Should().BeNull("successful runs should not have interrupt");
    }

    private async Task SetupTestServerAsync(FakeInterruptAgent fakeAgent)
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
/// Fake agent that emits interrupt events for testing.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated in tests")]
internal sealed class FakeInterruptAgent : AIAgent
{
    public override string? Description => "Agent for interrupt testing";

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

        if (userText?.Contains("trigger function approval", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Emit FunctionApprovalRequestContent which should trigger an interrupt
            var functionCall = new FunctionCallContent(
                "approval_123",
                "delete_important_file",
                new Dictionary<string, object?> { ["filename"] = "/etc/important.conf" });

            var approvalRequest = new FunctionApprovalRequestContent("approval_123", functionCall);

            yield return new AgentResponseUpdate
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Role = ChatRole.Assistant,
                Contents = [approvalRequest]
            };
        }
        else if (userText?.Contains("trigger user input request", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Emit UserInputRequestContent which should trigger an interrupt
            var inputRequest = new TestUserInputRequestContent("input_456")
            {
                RawRepresentation = JsonDocument.Parse("""{"prompt":"Please enter your API key","inputType":"password"}""").RootElement
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
            // Default: just return text (no interrupt)
            string messageId = Guid.NewGuid().ToString("N");
            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextContent("Hello from interrupt test agent")]
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

/// <summary>
/// Test implementation of UserInputRequestContent that can be instantiated directly.
/// </summary>
internal sealed class TestUserInputRequestContent : UserInputRequestContent
{
    public TestUserInputRequestContent(string id) : base(id)
    {
    }
}
