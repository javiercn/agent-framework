// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Components;
using AGUIDojoClient.Components.Shared;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

string serverUrl = builder.Configuration["SERVER_URL"] ?? "http://localhost:5018";

builder.Services.AddHttpClient("aguiserver", httpClient => httpClient.BaseAddress = new Uri(serverUrl));

// Register the DemoService for managing demo scenarios
builder.Services.AddSingleton<DemoService>();

// Register IChatClient for components like ChatSuggestions
builder.Services.AddChatClient(sp =>
{
    HttpClient httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("aguiserver");
    return new AGUIChatClient(httpClient, "ag-ui");
});

// Register a keyed AIAgent using AGUIChatClient
builder.Services.AddKeyedSingleton<AIAgent>("agentic-chat", (sp, key) =>
{
    HttpClient httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("aguiserver");
    AGUIChatClient aguiChatClient = new AGUIChatClient(httpClient, "ag-ui");
    return new ChatClientAgent(
        chatClient: aguiChatClient,
        name: "AgenticChatAssistant",
        description: "A helpful assistant for the agentic chat demo");
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
