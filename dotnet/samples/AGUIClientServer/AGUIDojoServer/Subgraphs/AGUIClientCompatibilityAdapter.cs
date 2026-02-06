// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.CompilerServices;
using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AGUIDojoServer.Subgraphs;

/// <summary>
/// A delegating agent that adapts between the AG-UI spec-compliant framework
/// and clients that use non-compliant mechanisms (like the AG-UI dojo).
/// </summary>
/// <remarks>
/// The AG-UI dojo client uses a homemade mechanism for human-in-the-loop:
/// <list type="bullet">
/// <item>
/// <description>Input: Sends user selections via <c>forwardedProps.command.resume</c> instead of the proper <c>Resume</c> field</description>
/// </item>
/// <item>
/// <description>Output: Expects <c>CUSTOM</c> events with <c>name: "on_interrupt"</c> instead of <c>RUN_FINISHED</c> with <c>Interrupt</c></description>
/// </item>
/// </list>
/// This adapter translates between these formats to allow the spec-compliant framework
/// to work with the non-compliant client.
/// </remarks>
internal sealed class AGUIClientCompatibilityAdapter : DelegatingAIAgent
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly ILogger<AGUIClientCompatibilityAdapter>? _logger;

    /// <summary>
    /// Stores pending interrupt requests by threadId so we can match resume responses.
    /// The AG-UI dojo client doesn't send back interrupt IDs, so we need to track them.
    /// </summary>
    private static readonly Dictionary<string, ExternalRequest> s_pendingInterrupts = new();

    public AGUIClientCompatibilityAdapter(AIAgent innerAgent, JsonSerializerOptions jsonSerializerOptions, ILogger<AGUIClientCompatibilityAdapter>? logger = null)
        : base(innerAgent)
    {
        _jsonSerializerOptions = jsonSerializerOptions;
        _logger = logger;
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Extract AGUIInput from ChatOptions within AgentRunOptions
        var chatOptions = (options as ChatClientAgentRunOptions)?.ChatOptions;
        var aguiInput = chatOptions?.GetAGUIInput();
        var threadId = aguiInput?.ThreadId ?? string.Empty;

        // Transform input: Extract forwardedProps.command.resume and add resume content to messages
        var transformedMessages = TransformInputMessages(messages, aguiInput);

        // Stream updates from the inner agent and transform output events
        await foreach (var update in base.RunCoreStreamingAsync(transformedMessages, session, options, cancellationToken))
        {
            // Check if this is a RequestInfoEvent (workflow interrupt request)
            // We need to intercept this BEFORE the hosting layer converts it to RUN_FINISHED with Interrupt
            if (TryTransformRequestInfoEvent(update, threadId, out var customEvent))
            {
                // Emit the CUSTOM event (for client to render buttons)
                // Let the hosting layer emit RUN_FINISHED automatically
                yield return customEvent!;
                continue;
            }

            // Pass through all other updates unchanged
            yield return update;
        }
    }

    /// <summary>
    /// Transforms input messages by extracting forwardedProps.command.resume and adding resume content.
    /// </summary>
    private IEnumerable<ChatMessage> TransformInputMessages(IEnumerable<ChatMessage> messages, RunAgentInput? aguiInput)
    {
        if (aguiInput is null)
        {
            Console.WriteLine("[ADAPTER DEBUG] aguiInput is null, returning original messages");
            return messages;
        }

        Console.WriteLine($"[ADAPTER DEBUG] ForwardedProperties: {aguiInput.ForwardedProperties}");

        // Check if forwardedProps.command.resume exists
        if (aguiInput.ForwardedProperties.ValueKind == JsonValueKind.Object &&
            aguiInput.ForwardedProperties.TryGetProperty("command", out var command) &&
            command.ValueKind == JsonValueKind.Object &&
            command.TryGetProperty("resume", out var resume))
        {
            Console.WriteLine($"[ADAPTER DEBUG] Found resume in forwardedProps: {resume}");

            // Parse the resume value (it may be a JSON string that needs parsing)
            JsonElement resumePayload;
            if (resume.ValueKind == JsonValueKind.String)
            {
                var resumeString = resume.GetString();
                if (!string.IsNullOrEmpty(resumeString))
                {
                    try
                    {
                        resumePayload = JsonDocument.Parse(resumeString).RootElement;
                    }
                    catch (JsonException)
                    {
                        // If it's not valid JSON, wrap it as a string value
                        resumePayload = JsonDocument.Parse($"\"{resumeString}\"").RootElement;
                    }
                }
                else
                {
                    return messages;
                }
            }
            else
            {
                resumePayload = resume;
            }

            Console.WriteLine($"[ADAPTER DEBUG] Parsed resume payload: {resumePayload}");

            // Look up the pending interrupt by threadId to get the correct RequestId
            var threadId = aguiInput.ThreadId;
            string interruptId;
            ExternalRequest? pendingRequest = null;

            lock (s_pendingInterrupts)
            {
                Console.WriteLine($"[ADAPTER DEBUG] Looking up pending interrupt for threadId='{threadId}'");
                Console.WriteLine($"[ADAPTER DEBUG] s_pendingInterrupts contains {s_pendingInterrupts.Count} entries: [{string.Join(", ", s_pendingInterrupts.Keys)}]");

                if (s_pendingInterrupts.TryGetValue(threadId, out pendingRequest))
                {
                    interruptId = pendingRequest.RequestId;
                    Console.WriteLine($"[ADAPTER DEBUG] Found pending interrupt for thread {threadId}: RequestId={interruptId}");
                    // Don't remove yet - let the workflow handle it
                }
                else
                {
                    interruptId = TryExtractInterruptId(resumePayload);
                    Console.WriteLine($"[ADAPTER DEBUG] No pending interrupt found for thread {threadId}, using fallback ID: {interruptId}");
                }
            }

            Console.WriteLine($"[ADAPTER DEBUG] Using interrupt ID: {interruptId}");

            // Create FunctionResultContent with the original ExternalRequest in RawRepresentation
            // This allows the framework to create a proper ExternalResponse with the correct PortInfo
            var resumeContent = CreateInterruptResponse(interruptId, resumePayload, pendingRequest);

            // Add the resume message to the messages list
            var messagesList = messages.ToList();
            messagesList.Add(new ChatMessage(ChatRole.User, [resumeContent]));

            Console.WriteLine($"[ADAPTER DEBUG] Added resume content message, total messages: {messagesList.Count}");
            return messagesList;
        }

        return messages;
    }

    /// <summary>
    /// Attempts to extract an interrupt ID from the resume payload.
    /// </summary>
    private static string TryExtractInterruptId(JsonElement payload)
    {
        // Try to get _portId if it exists in the payload
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("_portId", out var portId) &&
            portId.ValueKind == JsonValueKind.String)
        {
            return portId.GetString() ?? "workflow-interrupt";
        }

        // Default interrupt ID
        return "workflow-interrupt";
    }

    /// <summary>
    /// Attempts to transform a RequestInfoEvent into a CUSTOM event.
    /// The hosting layer would normally convert RequestInfoEvent to RUN_FINISHED with Interrupt,
    /// but the AG-UI dojo client expects CUSTOM events for button rendering.
    /// </summary>
    private bool TryTransformRequestInfoEvent(
        AgentResponseUpdate update,
        string threadId,
        out AgentResponseUpdate? customEvent)
    {
        customEvent = null;

        // Check if the RawRepresentation is a RequestInfoEvent
        var requestInfoEvent = ExtractRequestInfoEvent(update.RawRepresentation);
        if (requestInfoEvent is null)
        {
            return false;
        }

        // Get the request data
        var request = requestInfoEvent.Request;

        // Store the pending interrupt so we can match the resume later
        if (!string.IsNullOrEmpty(threadId))
        {
            lock (s_pendingInterrupts)
            {
                s_pendingInterrupts[threadId] = request;
                Console.WriteLine($"[ADAPTER DEBUG] Stored pending interrupt for thread {threadId}: RequestId={request.RequestId}, PortId={request.PortInfo.PortId}");
            }
        }

        // Debug: Print what we have
        Console.WriteLine($"[ADAPTER DEBUG] Request.Data type: {request.Data.GetType().FullName}");
        Console.WriteLine($"[ADAPTER DEBUG] Request.Data.TypeId: {request.Data.TypeId}");

        // Get the actual value - the Data property is a PortableValue wrapping the actual object
        // We need to serialize the ExternalRequest directly since it contains the PortableValue
        // which has the actual data in its Value property
        var requestJson = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        Console.WriteLine($"[ADAPTER DEBUG] Serialized request: {requestJson}");

        var requestElement = JsonDocument.Parse(requestJson).RootElement;

        // The data property should serialize to an object with the actual data
        // Note: using camelCase since JsonSerializerOptions uses camelCase naming policy
        JsonElement dataElement;
        if (requestElement.TryGetProperty("data", out var dataProp) &&
            dataProp.TryGetProperty("value", out var valueProp))
        {
            dataElement = valueProp;
            Console.WriteLine($"[ADAPTER DEBUG] Data.value property: {dataElement.GetRawText()}");
        }
        else
        {
            Console.WriteLine("[ADAPTER DEBUG] No data.value property found in request");
            dataElement = requestElement;
        }

        // Extract agent type from the data (use camelCase)
        string? agent = null;
        if (dataElement.ValueKind == JsonValueKind.Object &&
            dataElement.TryGetProperty("agent", out var agentProp))
        {
            agent = agentProp.GetString();
        }

        Console.WriteLine($"[ADAPTER DEBUG] Extracted agent: {agent}");

        // Build the CUSTOM event value in the format the client expects
        var customValue = BuildCustomEventValue(dataElement, agent);
        Console.WriteLine($"[ADAPTER DEBUG] Built custom value: {customValue.GetRawText()}");

        // CRITICAL: LangGraph sends the `value` as a JSON STRING (double-serialized), not an object!
        // We need to serialize the value object to a JSON string, then wrap that string as a JsonElement
        // JsonSerializer.Serialize(string) will properly escape and quote the string
        var valueAsJsonString = customValue.GetRawText();
        var serializedString = JsonSerializer.Serialize(valueAsJsonString);
        var valueAsJsonElement = JsonDocument.Parse(serializedString).RootElement;

        Console.WriteLine($"[ADAPTER DEBUG] Value as JSON string element: {valueAsJsonElement.GetRawText()}");

        // Create the CUSTOM event
        var aguiCustomEvent = new CustomEvent
        {
            Name = "on_interrupt",
            Value = valueAsJsonElement
        };

        // Create an AgentResponseUpdate with the CUSTOM event as RawRepresentation
        customEvent = new AgentResponseUpdate
        {
            RawRepresentation = aguiCustomEvent
        };

        return true;
    }

    /// <summary>
    /// Creates a FunctionResultContent for resuming an interrupted workflow.
    /// </summary>
    /// <param name="callId">The call ID from the original interrupt.</param>
    /// <param name="result">The user's response data.</param>
    /// <param name="externalRequest">The ExternalRequest from the pending interrupt (optional).</param>
    /// <returns>A FunctionResultContent that can be added to messages to resume the workflow.</returns>
    private static FunctionResultContent CreateInterruptResponse(
        string callId,
        object? result,
        ExternalRequest? externalRequest)
    {
        return new FunctionResultContent(callId, result)
        {
            RawRepresentation = externalRequest
        };
    }

    /// <summary>
    /// Recursively extracts a RequestInfoEvent from a RawRepresentation chain.
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
    /// Builds the CUSTOM event value in the format expected by the AG-UI dojo client.
    /// </summary>
    private JsonElement BuildCustomEventValue(JsonElement dataElement, string? agent)
    {
        // The client expects: { message, options, recommendation, agent }
        // Try to extract these from our SelectionRequest format (use camelCase)
        var valueDict = new Dictionary<string, object?>();

        if (dataElement.ValueKind == JsonValueKind.Object)
        {
            // Extract message
            if (dataElement.TryGetProperty("message", out var message))
            {
                valueDict["message"] = message.GetString();
            }

            // Extract options - convert to the format client expects
            if (dataElement.TryGetProperty("options", out var options) &&
                options.ValueKind == JsonValueKind.Array)
            {
                var optionsList = new List<Dictionary<string, object?>>();
                foreach (var option in options.EnumerateArray())
                {
                    var optionDict = ConvertOptionToClientFormat(option, agent);
                    optionsList.Add(optionDict);
                }
                valueDict["options"] = optionsList;
            }

            // Extract recommendation
            if (dataElement.TryGetProperty("recommendation", out var recommendation) &&
                recommendation.ValueKind == JsonValueKind.Object)
            {
                valueDict["recommendation"] = ConvertOptionToClientFormat(recommendation, agent);
            }

            // Extract agent
            if (dataElement.TryGetProperty("agent", out var agentProp))
            {
                valueDict["agent"] = agentProp.GetString();
            }
            else if (agent is not null)
            {
                valueDict["agent"] = agent;
            }
        }

        var jsonString = JsonSerializer.Serialize(valueDict, _jsonSerializerOptions);
        return JsonDocument.Parse(jsonString).RootElement;
    }

    /// <summary>
    /// Converts an option from our format to the client's expected format.
    /// Input JSON uses camelCase (from JsonSerializerOptions).
    /// </summary>
    private static Dictionary<string, object?> ConvertOptionToClientFormat(JsonElement option, string? agent)
    {
        var result = new Dictionary<string, object?>();

        if (option.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        // For flights: airline, departure, arrival, price, duration (camelCase)
        // Client expects: airline, departure, arrival, price, duration
        if (agent == "flights")
        {
            if (option.TryGetProperty("airline", out var airline))
                result["airline"] = airline.GetString();
            if (option.TryGetProperty("departure", out var departure))
                result["departure"] = departure.GetString();
            if (option.TryGetProperty("arrival", out var arrival))
                result["arrival"] = arrival.GetString();
            if (option.TryGetProperty("price", out var price))
                result["price"] = price.GetString();
            if (option.TryGetProperty("duration", out var duration))
                result["duration"] = duration.GetString();
        }
        // For hotels: name, location, pricePerNight, rating (camelCase)
        // Client expects: name, location, price_per_night, rating
        else if (agent == "hotels")
        {
            if (option.TryGetProperty("name", out var name))
                result["name"] = name.GetString();
            if (option.TryGetProperty("location", out var location))
                result["location"] = location.GetString();
            if (option.TryGetProperty("pricePerNight", out var pricePerNight))
                result["price_per_night"] = pricePerNight.GetString();
            if (option.TryGetProperty("rating", out var rating))
                result["rating"] = rating.GetString();
        }
        else
        {
            // Generic: copy all properties (already camelCase from serialization)
            foreach (var prop in option.EnumerateObject())
            {
                // Convert camelCase to snake_case for client if needed
                var key = prop.Name;
                result[key] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => prop.Value.GetRawText()
                };
            }
        }

        return result;
    }
}
