// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AGUI.Protocol;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests;

/// <summary>
/// Integration tests for the branching/time-travel feature via parentRunId.
/// </summary>
/// <remarks>
/// <para>
/// The AG-UI spec supports branching by allowing clients to send <c>parentRunId</c> in the
/// HTTP POST body (<see cref="RunAgentInput.ParentRunId"/>). This creates a git-like lineage
/// where a new run can branch from any prior run within the same thread.
/// </para>
/// <para>
/// These tests verify that:
/// <list type="bullet">
/// <item><description>The server receives <c>parentRunId</c> from the client request</description></item>
/// <item><description>The <c>parentRunId</c> is passed to the agent via <c>AdditionalProperties</c></description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class BranchingTests : IAsyncDisposable
{
    private WebApplication? _app;
    private HttpClient? _client;
    private BranchingTrackingAgent? _agent;

    [Fact]
    public async Task MapAGUI_ClientSendsParentRunId_AgentReceivesItAsync()
    {
        // Arrange
        await this.SetupTestServerAsync();

        var input = new RunAgentInput
        {
            ThreadId = "thread_main",
            RunId = "run_002",
            ParentRunId = "run_001", // Branching from run_001
            Messages =
            [
                new AGUIUserMessage
                {
                    Id = "m1",
                    Content = [new AGUITextInputContent { Text = "Continue from branch" }]
                }
            ]
        };

        // Act - Send raw HTTP POST with parentRunId
        var json = JsonSerializer.Serialize(input, AGUIJsonSerializerContext.Default.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await this._client!.PostAsync(this._client.BaseAddress!, content);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verify the agent received the parentRunId
        this._agent!.LastReceivedParentRunId.Should().Be("run_001");
        this._agent.LastReceivedThreadId.Should().Be("thread_main");
        this._agent.LastReceivedRunId.Should().Be("run_002");
    }

    [Fact]
    public async Task MapAGUI_ClientSendsNoParentRunId_AgentReceivesNullAsync()
    {
        // Arrange
        await this.SetupTestServerAsync();

        var input = new RunAgentInput
        {
            ThreadId = "thread_main",
            RunId = "run_001",
            ParentRunId = null, // First run has no parent
            Messages =
            [
                new AGUIUserMessage
                {
                    Id = "m1",
                    Content = [new AGUITextInputContent { Text = "Start conversation" }]
                }
            ]
        };

        // Act - Send raw HTTP POST without parentRunId
        var json = JsonSerializer.Serialize(input, AGUIJsonSerializerContext.Default.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await this._client!.PostAsync(this._client.BaseAddress!, content);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verify the agent received null parentRunId
        this._agent!.LastReceivedParentRunId.Should().BeNull();
        this._agent.LastReceivedRunId.Should().Be("run_001");
    }

    [Fact]
    public async Task MapAGUI_BranchingScenario_MultipleRunsWithLineageAsync()
    {
        // Arrange
        await this.SetupTestServerAsync();

        // First run - no parent
        var run1 = new RunAgentInput
        {
            ThreadId = "thread_main",
            RunId = "run_001",
            ParentRunId = null,
            Messages =
            [
                new AGUIUserMessage { Id = "m1", Content = [new AGUITextInputContent { Text = "Tell me about Paris" }] }
            ]
        };

        // Second run - branches from run_001
        var run2 = new RunAgentInput
        {
            ThreadId = "thread_main",
            RunId = "run_002",
            ParentRunId = "run_001",
            Messages =
            [
                new AGUIUserMessage { Id = "m2", Content = [new AGUITextInputContent { Text = "Actually, tell me about London" }] }
            ]
        };

        // Third run - also branches from run_001 (alternative branch)
        var run3 = new RunAgentInput
        {
            ThreadId = "thread_main",
            RunId = "run_003",
            ParentRunId = "run_001",
            Messages =
            [
                new AGUIUserMessage { Id = "m3", Content = [new AGUITextInputContent { Text = "What about Tokyo instead?" }] }
            ]
        };

        // Act & Assert - First run
        var json1 = JsonSerializer.Serialize(run1, AGUIJsonSerializerContext.Default.Options);
        using var content1 = new StringContent(json1, Encoding.UTF8, "application/json");
        using var response1 = await this._client!.PostAsync(this._client.BaseAddress!, content1);
        response1.IsSuccessStatusCode.Should().BeTrue();
        this._agent!.LastReceivedParentRunId.Should().BeNull();
        this._agent.LastReceivedRunId.Should().Be("run_001");

        // Act & Assert - Second run (branch from run_001)
        var json2 = JsonSerializer.Serialize(run2, AGUIJsonSerializerContext.Default.Options);
        using var content2 = new StringContent(json2, Encoding.UTF8, "application/json");
        using var response2 = await this._client!.PostAsync(this._client.BaseAddress!, content2);
        response2.IsSuccessStatusCode.Should().BeTrue();
        this._agent.LastReceivedParentRunId.Should().Be("run_001");
        this._agent.LastReceivedRunId.Should().Be("run_002");

        // Act & Assert - Third run (also branches from run_001)
        var json3 = JsonSerializer.Serialize(run3, AGUIJsonSerializerContext.Default.Options);
        using var content3 = new StringContent(json3, Encoding.UTF8, "application/json");
        using var response3 = await this._client!.PostAsync(this._client.BaseAddress!, content3);
        response3.IsSuccessStatusCode.Should().BeTrue();
        this._agent.LastReceivedParentRunId.Should().Be("run_001");
        this._agent.LastReceivedRunId.Should().Be("run_003");
    }

    private async Task SetupTestServerAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddAGUI();
        builder.Services.AddSingleton<BranchingTrackingAgent>();
        builder.WebHost.UseTestServer();

        this._app = builder.Build();
        this._agent = this._app.Services.GetRequiredService<BranchingTrackingAgent>();

        this._app.MapAGUI("/agent", this._agent);

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
/// Test agent that tracks the parentRunId received from the client for verification.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via dependency injection")]
internal sealed class BranchingTrackingAgent : AIAgent
{
    public string? LastReceivedParentRunId { get; private set; }
    public string? LastReceivedThreadId { get; private set; }
    public string? LastReceivedRunId { get; private set; }

    protected override string? IdCore => "branching-tracking-agent";

    public override string? Description => "A test agent that tracks parentRunId for branching tests";

    public override ValueTask<AgentSession> GetNewSessionAsync(CancellationToken cancellationToken = default) =>
        new(new FakeBranchingAgentSession());

    public override ValueTask<AgentSession> DeserializeSessionAsync(JsonElement serializedSession, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default) =>
        new(new FakeBranchingAgentSession(serializedSession, jsonSerializerOptions));

    private sealed class FakeBranchingAgentSession : InMemoryAgentSession
    {
        public FakeBranchingAgentSession()
            : base()
        {
        }

        public FakeBranchingAgentSession(JsonElement serializedSession, JsonSerializerOptions? jsonSerializerOptions = null)
            : base(serializedSession, jsonSerializerOptions)
        {
        }
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
        // Extract AG-UI properties using the new extension method
        var agentInput = (options as ChatClientAgentRunOptions)?.ChatOptions.GetAGUIInput();
        if (agentInput is not null)
        {
            this.LastReceivedParentRunId = agentInput.ParentRunId;
            this.LastReceivedThreadId = agentInput.ThreadId;
            this.LastReceivedRunId = agentInput.RunId;
        }
        else
        {
            this.LastReceivedParentRunId = null;
            this.LastReceivedThreadId = null;
            this.LastReceivedRunId = null;
        }

        string messageId = Guid.NewGuid().ToString("N");

        // Return a simple response
        foreach (string chunk in new[] { "Response", " ", "from", " ", "branching-tracking-agent" })
        {
            yield return new AgentResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextContent(chunk)]
            };

            await Task.Yield();
        }
    }
}
