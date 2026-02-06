// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;

/// <summary>
/// Extension methods for configuring AG-UI agent hosting with session persistence.
/// </summary>
public static class AGUIHostedAgentBuilderExtensions
{
    /// <summary>
    /// Configures the agent to use the specified session store for AG-UI hosting.
    /// </summary>
    /// <param name="builder">The hosted agent builder.</param>
    /// <param name="store">The session store instance to use for persisting agent sessions.</param>
    /// <returns>The same <see cref="IHostedAgentBuilder"/> instance so that additional calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// This registers a keyed session store specifically for AG-UI hosting. The store is keyed with
    /// the prefix <c>agui:</c> followed by the agent name to distinguish it from session stores used
    /// by other protocols (e.g., A2A).
    /// </para>
    /// <para>
    /// The session store enables server-side conversation persistence across HTTP requests using the
    /// AG-UI <c>threadId</c> as the conversation identifier.
    /// </para>
    /// </remarks>
    public static IHostedAgentBuilder WithAGUISessionStore(this IHostedAgentBuilder builder, AgentSessionStore store)
    {
        builder.ServiceCollection.AddKeyedSingleton<AgentSessionStore>($"agui:{builder.Name}", store);
        return builder;
    }

    /// <summary>
    /// Configures the agent to use an in-memory session store for AG-UI hosting.
    /// </summary>
    /// <param name="builder">The hosted agent builder.</param>
    /// <returns>The same <see cref="IHostedAgentBuilder"/> instance so that additional calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// This is a convenience method that configures the agent with an <see cref="InMemoryAgentSessionStore"/>
    /// for AG-UI hosting. This is suitable for development and testing scenarios.
    /// </para>
    /// <para>
    /// <strong>Warning:</strong> The in-memory store will lose all session data when the application restarts.
    /// For production use, consider using a durable storage implementation such as Redis, SQL Server,
    /// or Azure Cosmos DB.
    /// </para>
    /// </remarks>
    public static IHostedAgentBuilder WithAGUIInMemorySessionStore(this IHostedAgentBuilder builder)
    {
        return builder.WithAGUISessionStore(new InMemoryAgentSessionStore());
    }
}
