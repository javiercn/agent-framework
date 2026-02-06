// Copyright (c) Microsoft. All rights reserved.

// This sample demonstrates thinking events with AG-UI using a reasoning model.

#pragma warning disable OPENAI001 // Type is for evaluation purposes only

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using OpenAI.Responses;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient().AddLogging();
builder.Services.AddAGUI();

WebApplication app = builder.Build();

// Azure OpenAI Responses API endpoint (requires a deployment that supports reasoning)
string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

// Create the ResponsesClient using AzureOpenAIClient
AzureOpenAIClient azureOpenAIClient = new(
    new Uri(endpoint),
    new DefaultAzureCredential());

ResponsesClient responsesClient = azureOpenAIClient.GetResponsesClient(deploymentName);

// Create the AI agent using the ResponsesClient extension
// Configure reasoning effort and summary verbosity via clientFactory
AIAgent agent = responsesClient.AsAIAgent(
    name: "ThinkingAgent",
    instructions: "You are a helpful reasoning assistant. When answering complex questions, show your step-by-step thinking process.",
    clientFactory: innerClient => innerClient.AsBuilder()
        .ConfigureOptions(options =>
        {
            // Use RawRepresentationFactory to configure OpenAI Responses API reasoning options
            options.RawRepresentationFactory = _ => new CreateResponseOptions
            {
                ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = ResponseReasoningEffortLevel.Medium,
                    // Request reasoning summary to emit TextReasoningContent
                    ReasoningSummaryVerbosity = ResponseReasoningSummaryVerbosity.Auto
                }
            };
        })
        .Build());

// Map the AG-UI agent endpoint
app.MapAGUI("/", agent);

await app.RunAsync();
