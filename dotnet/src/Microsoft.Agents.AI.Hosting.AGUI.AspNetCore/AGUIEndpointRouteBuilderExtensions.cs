// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AGUI.Protocol;
using Microsoft.Agents.AI.AGUI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;

/// <summary>
/// Provides extension methods for mapping AG-UI agents to ASP.NET Core endpoints.
/// </summary>
public static class AGUIEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps an AG-UI agent endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the endpoint.</param>
    /// <param name="aiAgent">The agent instance.</param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> for the mapped endpoint.</returns>
    public static IEndpointConventionBuilder MapAGUI(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("route")] string pattern,
        AIAgent aiAgent)
    {
        return endpoints.MapPost(pattern, async ([FromBody] RunAgentInput? input, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (input is null)
            {
                return Results.BadRequest();
            }

            var jsonOptions = context.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
            var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

            var messages = input.Messages.AsChatMessages(jsonSerializerOptions).ToList();
            var clientTools = input.Tools?.AsAITools().ToList();

            // Handle resume payload if present (continuing from an interrupt)
            if (input.Resume is { } resume)
            {
                // Convert the resume to appropriate MEAI response content and add to messages
                var resumeContent = InterruptContentExtensions.FromAGUIResume(resume);
                messages.Add(new ChatMessage(ChatRole.User, [resumeContent]));
            }

            // Create run options with AG-UI context in AdditionalProperties
            var runOptions = new ChatClientAgentRunOptions
            {
                ChatOptions = new ChatOptions
                {
                    Tools = clientTools,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["ag_ui_state"] = input.State,
                        ["ag_ui_context"] = input.Context?.Select(c => new KeyValuePair<string, string>(c.Description, c.Value)).ToArray(),
                        ["ag_ui_forwarded_properties"] = input.ForwardedProperties,
                        ["ag_ui_thread_id"] = input.ThreadId,
                        ["ag_ui_run_id"] = input.RunId,
                        ["ag_ui_parent_run_id"] = input.ParentRunId
                    }
                }
            };

            // Run the agent and convert to AG-UI events
            var events = aiAgent.RunStreamingAsync(
                messages,
                options: runOptions,
                cancellationToken: cancellationToken)
                .AsChatResponseUpdatesAsync()
                .FilterServerToolsFromMixedToolInvocationsAsync(clientTools, cancellationToken)
                .AsAGUIEventStreamAsync(
                    input.ThreadId,
                    input.RunId,
                    jsonSerializerOptions,
                    cancellationToken);

            var sseLogger = context.RequestServices.GetRequiredService<ILogger<AGUIServerSentEventsResult>>();
            return new AGUIServerSentEventsResult(events, sseLogger);
        });
    }

    /// <summary>
    /// Maps an AG-UI agent endpoint with session persistence.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the endpoint.</param>
    /// <param name="aiAgent">The agent instance.</param>
    /// <param name="sessionStore">The session store for persisting agent sessions across requests.</param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> for the mapped endpoint.</returns>
    /// <remarks>
    /// <para>
    /// This overload enables server-side session persistence using the AG-UI <c>threadId</c> as the conversation identifier.
    /// The session store will be used to retrieve existing sessions and save updated sessions after each request.
    /// </para>
    /// <para>
    /// When using session persistence:
    /// <list type="bullet">
    /// <item><description>The server maintains conversation history via <see cref="AgentSession"/></description></item>
    /// <item><description>Clients can send only new messages (server has prior history)</description></item>
    /// <item><description>Sessions are saved after the streaming response completes</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static IEndpointConventionBuilder MapAGUI(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("route")] string pattern,
        AIAgent aiAgent,
        AgentSessionStore sessionStore)
    {
        return endpoints.MapPost(pattern, async ([FromBody] RunAgentInput? input, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (input is null)
            {
                return Results.BadRequest();
            }

            var jsonOptions = context.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
            var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

            var messages = input.Messages.AsChatMessages(jsonSerializerOptions).ToList();
            var clientTools = input.Tools?.AsAITools().ToList();

            // Handle resume payload if present (continuing from an interrupt)
            if (input.Resume is { } resume)
            {
                // Convert the resume to appropriate MEAI response content and add to messages
                var resumeContent = InterruptContentExtensions.FromAGUIResume(resume);
                messages.Add(new ChatMessage(ChatRole.User, [resumeContent]));
            }

            // Get or create session based on threadId
            var session = await sessionStore.GetSessionAsync(aiAgent, input.ThreadId, cancellationToken).ConfigureAwait(false);

            // Create run options with AG-UI context in AdditionalProperties
            var runOptions = new ChatClientAgentRunOptions
            {
                ChatOptions = new ChatOptions
                {
                    Tools = clientTools,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["ag_ui_state"] = input.State,
                        ["ag_ui_context"] = input.Context?.Select(c => new KeyValuePair<string, string>(c.Description, c.Value)).ToArray(),
                        ["ag_ui_forwarded_properties"] = input.ForwardedProperties,
                        ["ag_ui_thread_id"] = input.ThreadId,
                        ["ag_ui_run_id"] = input.RunId,
                        ["ag_ui_parent_run_id"] = input.ParentRunId
                    }
                }
            };

            // Run the agent with session and convert to AG-UI events
            var events = aiAgent.RunStreamingAsync(
                messages,
                session: session,
                options: runOptions,
                cancellationToken: cancellationToken)
                .AsChatResponseUpdatesAsync()
                .FilterServerToolsFromMixedToolInvocationsAsync(clientTools, cancellationToken)
                .AsAGUIEventStreamAsync(
                    input.ThreadId,
                    input.RunId,
                    jsonSerializerOptions,
                    cancellationToken);

            // Wrap the events stream to save session after completion
            var sessionPersistingEvents = WrapWithSessionPersistenceAsync(
                events,
                aiAgent,
                input.ThreadId,
                session,
                sessionStore,
                cancellationToken);

            var sseLogger = context.RequestServices.GetRequiredService<ILogger<AGUIServerSentEventsResult>>();
            return new AGUIServerSentEventsResult(sessionPersistingEvents, sseLogger);
        });
    }

    /// <summary>
    /// Wraps an event stream to persist the session after enumeration completes.
    /// </summary>
    private static async IAsyncEnumerable<BaseEvent> WrapWithSessionPersistenceAsync(
        IAsyncEnumerable<BaseEvent> events,
        AIAgent aiAgent,
        string threadId,
        AgentSession session,
        AgentSessionStore sessionStore,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var evt in events.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }

        // Save session after streaming completes successfully
        // Use a non-cancellable token to ensure session is saved even if original token is cancelled
        await sessionStore.SaveSessionAsync(aiAgent, threadId, session, CancellationToken.None).ConfigureAwait(false);
    }
}
