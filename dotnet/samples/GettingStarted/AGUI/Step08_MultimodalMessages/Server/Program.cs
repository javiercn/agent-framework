// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient().AddLogging();
builder.Services.AddAGUI();

WebApplication app = builder.Build();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

// Use a vision-capable model like gpt-4o or gpt-4o-mini
string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set. Use a vision-capable model like gpt-4o or gpt-4o-mini.");

// Create the AI agent with vision capabilities
ChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential())
    .GetChatClient(deploymentName);

AIAgent agent = chatClient.AsIChatClient().AsAIAgent(
    name: "VisionAssistant",
    instructions: """
        You are a helpful vision-capable assistant that can analyze images and other media content.
        When a user sends an image, describe what you see in detail, including:
        - Main subjects and objects
        - Colors and visual elements
        - Any text visible in the image
        - The overall scene or context

        If the user asks specific questions about the image, answer them based on what you observe.
        Be concise but thorough in your descriptions.
        """);

// Map the AG-UI agent endpoint
app.MapAGUI("/", agent);

Console.WriteLine("""

    ========================================
    AG-UI Multimodal Server
    ========================================

    This server demonstrates multimodal message support in the AG-UI protocol.
    It can process images, documents, and other media content sent by clients.

    Ensure you're using a vision-capable model (e.g., gpt-4o, gpt-4o-mini).

    Listening for connections...

    """);

await app.RunAsync();
