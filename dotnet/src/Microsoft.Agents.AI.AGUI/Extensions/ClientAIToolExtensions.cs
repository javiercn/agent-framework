// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using AGUI.Protocol;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.AGUI.Extensions;

internal static class ClientAIToolExtensions
{
    public static IEnumerable<AGUITool> AsAGUITools(this IEnumerable<AITool> tools)
    {
        if (tools is null)
        {
            yield break;
        }

        foreach (var tool in tools)
        {
            // Convert both AIFunctionDeclaration and AIFunction (which extends it) to AGUITool
            // For AIFunction, we send only the metadata (Name, Description, JsonSchema)
            // The actual executable implementation stays on the client side
            if (tool is AIFunctionDeclaration function)
            {
                yield return new AGUITool
                {
                    Name = function.Name,
                    Description = function.Description,
                    Parameters = function.JsonSchema
                };
            }
        }
    }
}
