// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Extensions.AI;

#pragma warning disable MEAI001 // Experimental API - FunctionApprovalRequestContent, UserInputRequestContent

namespace Microsoft.Agents.AI.AGUI.Extensions;

internal static class ClientInterruptContentExtensions
{
    private const string FunctionNameProperty = "functionName";
    private const string FunctionArgumentsProperty = "functionArguments";

    /// <summary>
    /// Converts an <see cref="AGUIInterrupt"/> to the appropriate MEAI content type.
    /// </summary>
    public static AIContent FromAGUIInterrupt(AGUIInterrupt interrupt)
    {
        // Check if this is a function approval interrupt by looking for functionName in payload
        if (interrupt.Payload is { } payload &&
            payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty(FunctionNameProperty, out var functionNameElement))
        {
            var functionName = functionNameElement.GetString() ?? string.Empty;
            IDictionary<string, object?>? arguments = null;

            if (payload.TryGetProperty(FunctionArgumentsProperty, out var argsElement))
            {
                arguments = JsonSerializer.Deserialize<IDictionary<string, object?>>(
                    argsElement.GetRawText(),
                    AGUIJsonSerializerContext.Default.IDictionaryStringObject);
            }

            var functionCall = new FunctionCallContent(
                interrupt.Id ?? Guid.NewGuid().ToString("N"),
                functionName,
                arguments);

            return new FunctionApprovalRequestContent(interrupt.Id ?? functionCall.CallId, functionCall)
            {
                RawRepresentation = interrupt
            };
        }

        // Otherwise, treat as a generic user input request using our custom derived type
        return new ClientAGUIUserInputRequestContent(interrupt.Id ?? Guid.NewGuid().ToString("N"))
        {
            RawRepresentation = interrupt
        };
    }
}

/// <summary>
/// AG-UI specific user input request content that can be instantiated directly.
/// </summary>
internal sealed class ClientAGUIUserInputRequestContent : UserInputRequestContent
{
    public ClientAGUIUserInputRequestContent(string id) : base(id)
    {
    }
}
