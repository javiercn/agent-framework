// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.AI;

namespace AGUI.Protocol;

/// <summary>
/// Provides extension methods for accessing AG-UI context from chat options.
/// </summary>
public static class AGUIOptionsExtensions
{
    /// <summary>
    /// Gets the AG-UI agent input from chat options additional properties.
    /// </summary>
    /// <param name="options">The chat options to extract AG-UI input from.</param>
    /// <returns>The <see cref="RunAgentInput"/> if present; otherwise, <see langword="null"/>.</returns>
    /// <remarks>
    /// This method extracts the <see cref="RunAgentInput"/> that was passed via
    /// <see cref="ChatOptions.AdditionalProperties"/> using the <see cref="AGUIPropertyNames.AgentInput"/> key.
    /// On the server side, the AG-UI hosting infrastructure automatically populates this when handling requests.
    /// On the client side, consumers can provide this via <see cref="ChatOptions.RawRepresentationFactory"/>.
    /// </remarks>
    public static RunAgentInput? GetAGUIInput(this ChatOptions? options)
    {
        if (options?.AdditionalProperties?.TryGetValue(AGUIPropertyNames.AgentInput, out var value) == true)
        {
            return value as RunAgentInput;
        }

        return null;
    }
}
