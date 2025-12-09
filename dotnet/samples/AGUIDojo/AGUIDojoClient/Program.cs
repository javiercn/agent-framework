// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using AGUIDojoClient.Components;
using AGUIDojoClient.Components.Shared;
using AGUIDojoClient.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

string serverUrl = builder.Configuration["SERVER_URL"] ?? "http://localhost:5018";

builder.Services.AddHttpClient("aguiserver", httpClient => httpClient.BaseAddress = new Uri(serverUrl));

// Register the DemoService for managing demo scenarios
builder.Services.AddSingleton<DemoService>();

// Register the BackgroundColorService for frontend tool support
builder.Services.AddSingleton<IBackgroundColorService, BackgroundColorService>();

// Register IChatClient for components like ChatSuggestions
builder.Services.AddChatClient(sp =>
{
    HttpClient httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("aguiserver");
    return new AGUIChatClient(httpClient, "agentic_chat");
});

// Register a keyed AIAgent using AGUIChatClient with frontend tools
builder.Services.AddKeyedSingleton<AIAgent>("agentic-chat", (sp, key) =>
{
    HttpClient httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("aguiserver");
    AGUIChatClient aguiChatClient = new AGUIChatClient(httpClient, "agentic_chat");

    // Get the background color service for the frontend tool
    IBackgroundColorService backgroundService = sp.GetRequiredService<IBackgroundColorService>();

    // Define the frontend tool that changes the background color
    [Description("Change the background color of the chat interface.")]
    string ChangeBackground([Description("The color to change the background to. Can be a color name (e.g., 'blue'), hex value (e.g., '#FF5733'), or RGB value (e.g., 'rgb(255,87,51)').")] string color)
    {
        backgroundService.SetColor(color);
        return $"Background color changed to {color}";
    }

    // Create frontend tools array
    AITool[] frontendTools = [AIFunctionFactory.Create(ChangeBackground)];

    return aguiChatClient.CreateAIAgent(
        name: "AgenticChatAssistant",
        description: "A helpful assistant for the agentic chat demo",
        tools: frontendTools);
});

// Register a keyed AIAgent for backend tool rendering (weather demo)
builder.Services.AddKeyedSingleton<AIAgent>("backend-tool-rendering", (sp, key) =>
{
    HttpClient httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("aguiserver");
    AGUIChatClient aguiChatClient = new AGUIChatClient(httpClient, "backend_tool_rendering");

    return aguiChatClient.CreateAIAgent(
        name: "BackendToolRenderingAssistant",
        description: "A helpful assistant that can look up weather information");
});

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
