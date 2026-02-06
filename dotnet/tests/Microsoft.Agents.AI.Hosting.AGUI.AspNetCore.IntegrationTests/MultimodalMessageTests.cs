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
/// Integration tests for multimodal message handling in the AG-UI protocol.
/// Verifies that binary content (images, audio, files) passes correctly through the stack.
/// </summary>
public sealed class MultimodalMessageTests : IAsyncDisposable
{
    private WebApplication? _app;
    private HttpClient? _client;

    [Fact]
    public async Task ClientCanSendMultimodalMessage_WithImageDataAsync()
    {
        // Arrange
        var capturingAgent = new FakeMultimodalCapturingAgent();
        await this.SetupTestServerAsync(capturingAgent);

        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Multimodal assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        // Create a multimodal message with text and image data
        byte[] imageBytes = [0x89, 0x50, 0x4E, 0x47]; // PNG magic bytes
        ChatMessage userMessage = new(ChatRole.User,
        [
            new TextContent("What's in this image?"),
            new DataContent(imageBytes, "image/png")
        ]);

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();
        DataContent? receivedDataContent = capturingAgent.CapturedContents.OfType<DataContent>().FirstOrDefault();
        receivedDataContent.Should().NotBeNull();
        receivedDataContent!.MediaType.Should().Be("image/png");
        receivedDataContent.Data.ToArray().Should().BeEquivalentTo(imageBytes);
    }

    [Fact]
    public async Task ClientCanSendMultimodalMessage_WithUrlReferenceAsync()
    {
        // Arrange
        var capturingAgent = new FakeMultimodalCapturingAgent();
        await this.SetupTestServerAsync(capturingAgent);

        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Multimodal assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        // Create a multimodal message with URL reference
        ChatMessage userMessage = new(ChatRole.User,
        [
            new TextContent("Analyze this document"),
            new UriContent(new Uri("https://example.com/document.pdf"), "application/pdf")
        ]);

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();
        UriContent? receivedUriContent = capturingAgent.CapturedContents.OfType<UriContent>().FirstOrDefault();
        receivedUriContent.Should().NotBeNull();
        receivedUriContent!.MediaType.Should().Be("application/pdf");
        receivedUriContent.Uri.Should().Be(new Uri("https://example.com/document.pdf"));
    }

    [Fact]
    public async Task ClientCanSendMultimodalMessage_WithMultipleContentsAsync()
    {
        // Arrange
        var capturingAgent = new FakeMultimodalCapturingAgent();
        await this.SetupTestServerAsync(capturingAgent);

        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Multimodal assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        // Create a multimodal message with multiple content types
        byte[] imageBytes1 = [1, 2, 3];
        byte[] imageBytes2 = [4, 5, 6];
        ChatMessage userMessage = new(ChatRole.User,
        [
            new TextContent("Compare these images"),
            new DataContent(imageBytes1, "image/jpeg"),
            new DataContent(imageBytes2, "image/png")
        ]);

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();
        capturingAgent.CapturedContents.Should().HaveCount(3);

        TextContent textContent = capturingAgent.CapturedContents.OfType<TextContent>().Should().ContainSingle().Which;
        textContent.Text.Should().Be("Compare these images");

        List<DataContent> dataContents = capturingAgent.CapturedContents.OfType<DataContent>().ToList();
        dataContents.Should().HaveCount(2);
        dataContents[0].MediaType.Should().Be("image/jpeg");
        dataContents[0].Data.ToArray().Should().BeEquivalentTo(imageBytes1);
        dataContents[1].MediaType.Should().Be("image/png");
        dataContents[1].Data.ToArray().Should().BeEquivalentTo(imageBytes2);
    }

    [Fact]
    public async Task ClientCanSendMultimodalMessage_PreservesFilenameMetadataAsync()
    {
        // Arrange
        var capturingAgent = new FakeMultimodalCapturingAgent();
        await this.SetupTestServerAsync(capturingAgent);

        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Multimodal assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        // Create a multimodal message with filename metadata
        byte[] audioBytes = [0xFF, 0xFB, 0x90]; // MP3 frame header
        DataContent audioContent = new(audioBytes, "audio/mpeg");
        audioContent.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            ["filename"] = "recording.mp3"
        };

        ChatMessage userMessage = new(ChatRole.User,
        [
            new TextContent("Transcribe this audio"),
            audioContent
        ]);

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();
        DataContent? receivedDataContent = capturingAgent.CapturedContents.OfType<DataContent>().FirstOrDefault();
        receivedDataContent.Should().NotBeNull();
        receivedDataContent!.MediaType.Should().Be("audio/mpeg");
        receivedDataContent.AdditionalProperties.Should().ContainKey("filename");
        receivedDataContent.AdditionalProperties!["filename"].Should().Be("recording.mp3");
    }

    [Fact]
    public async Task TextOnlyMessage_StillWorksWithMultimodalInfrastructureAsync()
    {
        // Arrange
        var capturingAgent = new FakeMultimodalCapturingAgent();
        await this.SetupTestServerAsync(capturingAgent);

        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.AsAIAgent(instructions: null, name: "assistant", description: "Multimodal assistant", tools: []);
        ChatClientAgentSession? session = (ChatClientAgentSession)await agent.GetNewSessionAsync();

        // Create a simple text message
        ChatMessage userMessage = new(ChatRole.User, "Hello, this is just text!");

        List<AgentResponseUpdate> updates = [];

        // Act
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync([userMessage], session, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();
        TextContent? receivedTextContent = capturingAgent.CapturedContents.OfType<TextContent>().FirstOrDefault();
        receivedTextContent.Should().NotBeNull();
        receivedTextContent!.Text.Should().Be("Hello, this is just text!");
    }

    private async Task SetupTestServerAsync(AIAgent agent)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddAGUI();
        builder.Services.AddSingleton(agent);

        this._app = builder.Build();

        this._app.MapAGUI("/agent", agent);

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
/// An agent that captures and inspects multimodal content from incoming messages.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated for testing")]
internal sealed class FakeMultimodalCapturingAgent : AIAgent
{
    protected override string? IdCore => "fake-multimodal-agent";

    public override string? Description => "A fake agent that captures multimodal content for testing";

    public List<AIContent> CapturedContents { get; } = [];

    public override ValueTask<AgentSession> GetNewSessionAsync(CancellationToken cancellationToken = default) =>
        new(new FakeInMemoryAgentSession());

    public override ValueTask<AgentSession> DeserializeSessionAsync(JsonElement serializedSession, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default) =>
        new(new FakeInMemoryAgentSession(serializedSession, jsonSerializerOptions));

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
        // Clear previous captured contents
        this.CapturedContents.Clear();

        // Capture all content from the last user message
        ChatMessage? lastUserMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
        if (lastUserMessage != null)
        {
            foreach (AIContent content in lastUserMessage.Contents)
            {
                this.CapturedContents.Add(content);
            }
        }

        string messageId = Guid.NewGuid().ToString("N");

        // Return a simple response
        yield return new AgentResponseUpdate
        {
            MessageId = messageId,
            Role = ChatRole.Assistant,
            Contents = [new TextContent("Received multimodal content")]
        };

        await Task.Yield();
    }

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
}
