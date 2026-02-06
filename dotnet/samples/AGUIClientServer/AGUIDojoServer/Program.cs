// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoServer;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders | HttpLoggingFields.RequestBody
        | HttpLoggingFields.ResponsePropertiesAndHeaders | HttpLoggingFields.ResponseBody;
    logging.RequestBodyLogLimit = int.MaxValue;
    logging.ResponseBodyLogLimit = int.MaxValue;
});

builder.Services.AddHttpClient().AddLogging();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Add(AGUIDojoServerSerializerContext.Default));
builder.Services.AddAGUI();

WebApplication app = builder.Build();

app.UseHttpLogging();

// Initialize the factory
ChatClientAgentFactory.Initialize(app.Configuration);

// Map the AG-UI agent endpoints for different scenarios
app.MapAGUI("/agentic_chat", ChatClientAgentFactory.CreateAgenticChat());

app.MapAGUI("/backend_tool_rendering", ChatClientAgentFactory.CreateBackendToolRendering());

app.MapAGUI("/human_in_the_loop", ChatClientAgentFactory.CreateHumanInTheLoop());

app.MapAGUI("/tool_based_generative_ui", ChatClientAgentFactory.CreateToolBasedGenerativeUI());

var jsonOptions = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
app.MapAGUI("/agentic_generative_ui", ChatClientAgentFactory.CreateAgenticUI(jsonOptions.Value.SerializerOptions));

app.MapAGUI("/shared_state", ChatClientAgentFactory.CreateSharedState(jsonOptions.Value.SerializerOptions));

app.MapAGUI("/predictive_state_updates", ChatClientAgentFactory.CreatePredictiveStateUpdates(jsonOptions.Value.SerializerOptions));

// Use session persistence for subgraphs to support workflow resume
// Using debug wrapper to trace session serialization/deserialization
var subgraphsSessionStore = new DebugAgentSessionStore();
app.MapAGUI("/subgraphs", ChatClientAgentFactory.CreateSubgraphs(jsonOptions.Value.SerializerOptions), subgraphsSessionStore);

await app.RunAsync();

public partial class Program;
