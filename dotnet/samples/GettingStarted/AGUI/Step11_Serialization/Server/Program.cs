// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient().AddLogging();
builder.Services.AddAGUI();

WebApplication app = builder.Build();

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

// Create the AI agent
ChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential())
    .GetChatClient(deploymentName);

AIAgent agent = chatClient.AsIChatClient().AsAIAgent(
    name: "AGUISerializationAgent",
    instructions: "You are a helpful assistant. Keep track of our conversation and remember what we've discussed.");

// Create an in-memory session store for server-side session persistence
// In production, you would use a durable store like Redis, SQL Server, or Cosmos DB
InMemoryAgentSessionStore sessionStore = new();

Console.WriteLine("AG-UI Server with Session Persistence");
Console.WriteLine("======================================");
Console.WriteLine();
Console.WriteLine("This server demonstrates server-side session persistence:");
Console.WriteLine("- Sessions are stored using the AG-UI threadId as the key");
Console.WriteLine("- Conversation history is maintained on the server");
Console.WriteLine("- Clients can continue conversations across requests");
Console.WriteLine();
Console.WriteLine($"Listening at: {app.Urls.FirstOrDefault() ?? "http://localhost:5000"}");
Console.WriteLine();

// Map the AG-UI agent endpoint WITH session store
// This enables server-side session persistence across HTTP requests
app.MapAGUI("/", agent, sessionStore);

await app.RunAsync();
