// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using AGUI.Protocol;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.Extensions;

internal static class ServerAIToolExtensions
{
    public static IEnumerable<AITool> AsAITools(this IEnumerable<AGUITool> tools)
    {
        if (tools is null)
        {
            yield break;
        }

        foreach (var tool in tools)
        {
            // Create a function declaration from the AG-UI tool definition
            // Note: These are declaration-only and cannot be invoked, as the actual
            // implementation exists on the client side
            yield return AIFunctionFactory.CreateDeclaration(
                name: tool.Name,
                description: tool.Description,
                jsonSchema: tool.Parameters);
        }
    }
}
