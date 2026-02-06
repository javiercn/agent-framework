// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.Extensions;

/// <summary>
/// Extension methods for handling workflow interrupts in AG-UI.
/// </summary>
internal static class WorkflowInterruptExtensions
{
    // Property names for interrupt payload
    private const string PortIdProperty = "_portId";
    private const string RequestTypeProperty = "_requestType";
    private const string ResponseTypeProperty = "_responseType";
    private const string DataProperty = "data";

    /// <summary>
    /// Attempts to extract a <see cref="RequestInfoEvent"/> from the RawRepresentation chain.
    /// </summary>
    /// <param name="rawRepresentation">The raw representation to search.</param>
    /// <param name="requestInfoEvent">The extracted event, if found.</param>
    /// <returns>True if a RequestInfoEvent was found; otherwise, false.</returns>
    public static bool TryExtractRequestInfoEvent(
        object? rawRepresentation,
        out RequestInfoEvent? requestInfoEvent)
    {
        requestInfoEvent = ExtractRequestInfoEvent(rawRepresentation);
        return requestInfoEvent is not null;
    }

    /// <summary>
    /// Recursively extracts a <see cref="RequestInfoEvent"/> from a RawRepresentation chain.
    /// </summary>
    private static RequestInfoEvent? ExtractRequestInfoEvent(object? rawRepresentation)
    {
        return rawRepresentation switch
        {
            RequestInfoEvent evt => evt,
            AgentResponseUpdate update => ExtractRequestInfoEvent(update.RawRepresentation),
            ChatResponseUpdate chatUpdate => ExtractRequestInfoEvent(chatUpdate.RawRepresentation),
            _ => null
        };
    }

    /// <summary>
    /// Converts a workflow <see cref="ExternalRequest"/> to an <see cref="AGUIInterrupt"/>.
    /// </summary>
    /// <param name="request">The external request from the workflow.</param>
    /// <param name="jsonSerializerOptions">JSON serialization options.</param>
    /// <returns>An AG-UI interrupt representing the workflow request.</returns>
    public static AGUIInterrupt ToAGUIInterrupt(
        ExternalRequest request,
        JsonSerializerOptions jsonSerializerOptions)
    {
        // Get the underlying data from the request
        var requestData = request.Data.AsType(typeof(object));

        // Build payload that includes both the request data and metadata needed for response
        var payloadDict = new Dictionary<string, object?>
        {
            [PortIdProperty] = request.PortInfo.PortId,
            [RequestTypeProperty] = request.PortInfo.RequestType.TypeName,
            [ResponseTypeProperty] = request.PortInfo.ResponseType.TypeName,
            [DataProperty] = requestData
        };

        var payloadJson = JsonSerializer.Serialize(
            payloadDict,
            jsonSerializerOptions.GetTypeInfo(typeof(Dictionary<string, object?>)));

        return new AGUIInterrupt
        {
            Id = request.RequestId,
            Payload = JsonDocument.Parse(payloadJson).RootElement
        };
    }

    /// <summary>
    /// Converts an AG-UI resume to a <see cref="FunctionResultContent"/> for workflow processing.
    /// </summary>
    /// <param name="resume">The AG-UI resume payload.</param>
    /// <returns>A FunctionResultContent that can be added to messages.</returns>
    public static FunctionResultContent ToFunctionResultContent(AGUIResume resume)
    {
        // The InterruptId corresponds to the RequestId from the original ExternalRequest
        // The Payload contains the user's response data
        return new FunctionResultContent(
            resume.InterruptId ?? string.Empty,
            resume.Payload)
        {
            RawRepresentation = resume
        };
    }

    /// <summary>
    /// Attempts to create an <see cref="ExternalResponse"/> from an AG-UI resume.
    /// </summary>
    /// <param name="resume">The AG-UI resume payload.</param>
    /// <param name="jsonSerializerOptions">JSON serialization options.</param>
    /// <returns>An ExternalResponse if successful; otherwise, null.</returns>
    public static ExternalResponse? TryCreateExternalResponse(
        AGUIResume resume,
        JsonSerializerOptions jsonSerializerOptions)
    {
        if (resume.Payload is not { } payload || payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Extract port info from the original interrupt payload (stored in session or passed through)
        // For now, we create a response with the data; the workflow will need to match by RequestId
        var responseData = new PortableValue(payload);

        // We need the original PortInfo to create a proper ExternalResponse
        // This requires storing the pending request context in the session
        // For now, return null to indicate this needs workflow-side handling
        return null;
    }
}
