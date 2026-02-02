// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Extensions.AI;

#pragma warning disable MEAI001 // Experimental API - FunctionApprovalRequestContent, UserInputRequestContent

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.Extensions;

/// <summary>
/// AG-UI specific user input request content that can be instantiated directly.
/// </summary>
internal sealed class ServerAGUIUserInputRequestContent : UserInputRequestContent
{
    public ServerAGUIUserInputRequestContent(string id) : base(id)
    {
    }
}

/// <summary>
/// AG-UI specific user input response content that can be instantiated directly.
/// </summary>
internal sealed class ServerAGUIUserInputResponseContent : UserInputResponseContent
{
    public ServerAGUIUserInputResponseContent(string id) : base(id)
    {
    }
}

/// <summary>
/// Extension methods for converting between AG-UI interrupt types and MEAI content types.
/// </summary>
internal static class ServerInterruptContentExtensions
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
        return new ServerAGUIUserInputRequestContent(interrupt.Id ?? Guid.NewGuid().ToString("N"))
        {
            RawRepresentation = interrupt
        };
    }

    /// <summary>
    /// Converts a <see cref="FunctionApprovalRequestContent"/> to an <see cref="AGUIInterrupt"/>.
    /// </summary>
    public static AGUIInterrupt ToAGUIInterrupt(
        FunctionApprovalRequestContent approvalRequest,
        JsonSerializerOptions jsonSerializerOptions)
    {
        // Build payload with function name and arguments
        var payloadDict = new Dictionary<string, object?>
        {
            [FunctionNameProperty] = approvalRequest.FunctionCall.Name,
            [FunctionArgumentsProperty] = approvalRequest.FunctionCall.Arguments
        };

        var payloadJson = JsonSerializer.Serialize(
            payloadDict,
            jsonSerializerOptions.GetTypeInfo(typeof(Dictionary<string, object?>)));

        return new AGUIInterrupt
        {
            Id = approvalRequest.Id,
            Payload = JsonDocument.Parse(payloadJson).RootElement
        };
    }

    /// <summary>
    /// Converts a <see cref="UserInputRequestContent"/> to an <see cref="AGUIInterrupt"/>.
    /// </summary>
    public static AGUIInterrupt ToAGUIInterrupt(UserInputRequestContent inputRequest)
    {
        // If the RawRepresentation is already an AGUIInterrupt, return it
        if (inputRequest.RawRepresentation is AGUIInterrupt existingInterrupt)
        {
            return existingInterrupt;
        }

        // If RawRepresentation is a JsonElement, use it as the payload
        if (inputRequest.RawRepresentation is JsonElement jsonPayload)
        {
            return new AGUIInterrupt
            {
                Id = inputRequest.Id,
                Payload = jsonPayload
            };
        }

        // Otherwise, create a minimal interrupt with just the ID
        return new AGUIInterrupt
        {
            Id = inputRequest.Id
        };
    }

    /// <summary>
    /// Converts a <see cref="FunctionApprovalResponseContent"/> to an <see cref="AGUIResume"/>.
    /// </summary>
    public static AGUIResume ToAGUIResume(
        FunctionApprovalResponseContent approvalResponse,
        JsonSerializerOptions jsonSerializerOptions)
    {
        var payloadDict = new Dictionary<string, object?>
        {
            ["approved"] = approvalResponse.Approved
        };

        var payloadJson = JsonSerializer.Serialize(
            payloadDict,
            jsonSerializerOptions.GetTypeInfo(typeof(Dictionary<string, object?>)));

        return new AGUIResume
        {
            InterruptId = approvalResponse.Id,
            Payload = JsonDocument.Parse(payloadJson).RootElement
        };
    }

    /// <summary>
    /// Converts a <see cref="UserInputResponseContent"/> to an <see cref="AGUIResume"/>.
    /// </summary>
    public static AGUIResume ToAGUIResume(
        UserInputResponseContent inputResponse,
        JsonSerializerOptions jsonSerializerOptions)
    {
        // Use RawRepresentation or AdditionalProperties to get the response data
        JsonElement? payload = null;

        if (inputResponse.RawRepresentation is JsonElement jsonElement)
        {
            payload = jsonElement;
        }
        else if (inputResponse.AdditionalProperties?.TryGetValue("response", out var responseValue) == true)
        {
            if (responseValue is JsonElement jsonResponseElement)
            {
                payload = jsonResponseElement;
            }
            else if (responseValue is string stringResponse)
            {
                var payloadDict = new Dictionary<string, object?> { ["response"] = stringResponse };
                var payloadJson = JsonSerializer.Serialize(
                    payloadDict,
                    jsonSerializerOptions.GetTypeInfo(typeof(Dictionary<string, object?>)));
                payload = JsonDocument.Parse(payloadJson).RootElement;
            }
        }

        return new AGUIResume
        {
            InterruptId = inputResponse.Id,
            Payload = payload
        };
    }

    /// <summary>
    /// Converts an <see cref="AGUIResume"/> to the appropriate MEAI response content.
    /// </summary>
    public static AIContent FromAGUIResume(AGUIResume resume, AIContent? originalInterrupt = null)
    {
        // If we have the original interrupt and it was a function approval request
        if (originalInterrupt is FunctionApprovalRequestContent approvalRequest)
        {
            var isApproved = false;
            if (resume.Payload is { } payload &&
                payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty("approved", out var approvedElement))
            {
                isApproved = approvedElement.GetBoolean();
            }

            return new FunctionApprovalResponseContent(resume.InterruptId ?? string.Empty, isApproved, approvalRequest.FunctionCall)
            {
                RawRepresentation = resume
            };
        }

        // Otherwise, treat as user input response using our custom derived type
        return new ServerAGUIUserInputResponseContent(resume.InterruptId ?? string.Empty)
        {
            RawRepresentation = resume,
            AdditionalProperties = resume.Payload is { } p ? new AdditionalPropertiesDictionary { ["payload"] = p } : null
        };
    }
}
