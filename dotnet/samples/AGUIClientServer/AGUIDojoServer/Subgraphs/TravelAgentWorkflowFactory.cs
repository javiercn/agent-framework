// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUI.Protocol;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AGUIDojoServer.Subgraphs;

/// <summary>
/// Factory for creating the Travel Agent workflow using the supervisor pattern.
/// </summary>
/// <remarks>
/// The workflow follows the supervisor pattern from LangGraph:
/// 1. Supervisor: ChatProtocolExecutor that routes based on state and handles interrupt responses
/// 2. Flights Agent: searches flights, interrupts for user selection
/// 3. Hotels Agent: searches hotels, interrupts for user selection
/// 4. Experiences Agent: finds activities (no interrupt)
///
/// After each agent completes, control returns to the supervisor which checks
/// the state and routes to the next appropriate agent.
/// </remarks>
public static class TravelAgentWorkflowFactory
{
    private const string StateKey = "travelState";
    private const string StateScope = "Travel";

    /// <summary>
    /// Creates the Travel Agent workflow with supervisor-based routing and interrupt-based selection.
    /// </summary>
    /// <returns>The compiled workflow.</returns>
    public static Workflow Create()
    {
        // Create executors
        var supervisor = new SupervisorExecutor();
        var flightAgent = new FlightsExecutor();
        var flightRequest = RequestPort.Create<FlightSelectionRequest, Flight>("SelectFlight");
        var hotelAgent = new HotelsExecutor();
        var hotelRequest = RequestPort.Create<HotelSelectionRequest, Hotel>("SelectHotel");
        var experiencesAgent = new ExperiencesExecutor();

        // Build workflow with supervisor routing pattern
        var builder = new WorkflowBuilder(supervisor);

        // Type-based edges for handling interrupt responses
        // When a Flight/Hotel is received, route it back to supervisor for handling
        builder.AddEdge<Flight>(
            supervisor,
            supervisor,
            flight => flight is not null);
        builder.AddEdge<Hotel>(
            supervisor,
            supervisor,
            hotel => hotel is not null);

        // Flights path: supervisor → flightAgent → flightRequest → supervisor
        builder.AddEdge(supervisor, flightAgent);
        builder.AddEdge(flightAgent, flightRequest);
        builder.AddEdge(flightRequest, supervisor);

        // Hotels path: supervisor → hotelAgent → hotelRequest → supervisor
        builder.AddEdge(supervisor, hotelAgent);
        builder.AddEdge(hotelAgent, hotelRequest);
        builder.AddEdge(hotelRequest, supervisor);

        // Experiences path: supervisor → experiencesAgent → supervisor
        builder.AddEdge(supervisor, experiencesAgent);
        builder.AddEdge(experiencesAgent, supervisor);

        return builder.Build();
    }

    /// <summary>
    /// Gets or initializes the travel agent state from the workflow context.
    /// </summary>
    internal static async ValueTask<TravelAgentState> GetStateAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        return await context.ReadOrInitStateAsync(StateKey, () => new TravelAgentState(), StateScope, cancellationToken);
    }

    /// <summary>
    /// Updates the travel agent state in the workflow context.
    /// </summary>
    internal static async ValueTask UpdateStateAsync(IWorkflowContext context, TravelAgentState state, CancellationToken cancellationToken)
    {
        await context.QueueStateUpdateAsync(StateKey, state, StateScope, cancellationToken);
    }

    // JsonSerializerOptions for state snapshot serialization (camelCase to match AG-UI expectations)
    private static readonly JsonSerializerOptions s_stateJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Emits a STATE_SNAPSHOT event to update the AG-UI client's shared state.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="state">The current travel agent state.</param>
    /// <param name="activeAgent">The currently active agent name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal static async ValueTask EmitStateSnapshotAsync(
        IWorkflowContext context,
        TravelAgentState state,
        string activeAgent,
        CancellationToken cancellationToken)
    {
        // Convert to AG-UI format
        var aguiState = AGUIStateSnapshot.FromTravelAgentState(state, activeAgent);

        // Serialize to JsonElement
        var jsonString = JsonSerializer.Serialize(aguiState, s_stateJsonOptions);
        var jsonElement = JsonDocument.Parse(jsonString).RootElement.Clone();

        // Create and emit the StateSnapshotEvent
        var stateSnapshotEvent = new StateSnapshotEvent
        {
            Snapshot = jsonElement
        };

        // Wrap in AgentResponseUpdate with RawRepresentation = BaseEvent
        // The hosting layer will extract and emit this directly as an AG-UI event
        await context.AddEventAsync(
            new AgentResponseUpdateEvent(
                "StateSnapshot",
                new AgentResponseUpdate { RawRepresentation = stateSnapshotEvent }),
            cancellationToken);
    }
}

/// <summary>
/// Supervisor executor that uses ChatProtocolExecutor for AG-UI compatibility.
/// Routes based on state and handles interrupt responses via ConfigureRoutes handlers.
/// </summary>
internal sealed class SupervisorExecutor() : ChatProtocolExecutor("Supervisor")
{
    protected override Microsoft.Agents.AI.Workflows.RouteBuilder ConfigureRoutes(Microsoft.Agents.AI.Workflows.RouteBuilder routeBuilder)
    {
        return base.ConfigureRoutes(routeBuilder)
            .AddHandler<Flight>(this.HandleSelectedFlightAsync)
            .AddHandler<Hotel>(this.HandleSelectedHotelAsync);
    }

    private async ValueTask HandleSelectedFlightAsync(Flight flight, IWorkflowContext context, CancellationToken cancellationToken)
    {
        var state = await TravelAgentWorkflowFactory.GetStateAsync(context, cancellationToken);
        state.Itinerary.SelectedFlight = flight;
        await TravelAgentWorkflowFactory.UpdateStateAsync(context, state, cancellationToken);

        // Emit state snapshot so the UI updates the itinerary
        await TravelAgentWorkflowFactory.EmitStateSnapshotAsync(context, state, "supervisor", cancellationToken);

        // Emit confirmation message
        await context.AddEventAsync(new AgentResponseUpdateEvent(
            this.Id,
            new AgentResponseUpdate(ChatRole.Assistant, $"Flights Agent: Great! I'll book you the {flight.Airline} flight from {flight.Departure} to {flight.Arrival}.")),
            cancellationToken);

        // Route to hotel agent
        await context.SendMessageAsync(state, "HotelsExecutor", cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask HandleSelectedHotelAsync(Hotel hotel, IWorkflowContext context, CancellationToken cancellationToken)
    {
        var state = await TravelAgentWorkflowFactory.GetStateAsync(context, cancellationToken);
        state.Itinerary.SelectedHotel = hotel;
        await TravelAgentWorkflowFactory.UpdateStateAsync(context, state, cancellationToken);

        // Emit state snapshot so the UI updates the itinerary
        await TravelAgentWorkflowFactory.EmitStateSnapshotAsync(context, state, "supervisor", cancellationToken);

        // Emit confirmation message
        await context.AddEventAsync(new AgentResponseUpdateEvent(
            this.Id,
            new AgentResponseUpdate(ChatRole.Assistant, $"Hotels Agent: Excellent choice! You'll love {hotel.Name} in {hotel.Location}.")),
            cancellationToken);

        // Route to experiences agent
        await context.SendMessageAsync(state, "ExperiencesExecutor", cancellationToken).ConfigureAwait(false);
    }

    // JsonSerializerOptions to match the camelCase JSON from the AG-UI dojo client
    private static readonly System.Text.Json.JsonSerializerOptions s_jsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    protected override async ValueTask TakeTurnAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        bool? emitEvents,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[SUPERVISOR DEBUG] TakeTurnAsync called with {messages.Count} messages");

        // FIRST: Check for interrupt responses from the adapter
        // The adapter stores ExternalRequest in RawRepresentation and the resume payload in Result
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var message = messages[i];
            Console.WriteLine($"[SUPERVISOR DEBUG] Message {i}: Role={message.Role}, Contents={message.Contents.Count}");
            foreach (var content in message.Contents)
            {
                Console.WriteLine($"[SUPERVISOR DEBUG]   Content type: {content.GetType().Name}");
                if (content is FunctionResultContent frcDebug)
                {
                    Console.WriteLine($"[SUPERVISOR DEBUG]   FRC: CallId={frcDebug.CallId}, Result type={frcDebug.Result?.GetType().Name}, RawRepresentation type={frcDebug.RawRepresentation?.GetType().Name}");
                }

                if (content is FunctionResultContent frc && frc.RawRepresentation is ExternalRequest externalRequest)
                {
                    Console.WriteLine("[SUPERVISOR DEBUG] Processing FRC with ExternalRequest");
                    // Deserialize the result to the expected response type
                    var responseType = externalRequest.PortInfo.ResponseType;
                    Console.WriteLine($"[SUPERVISOR DEBUG] ResponseType: TypeName={responseType.TypeName}, AssemblyName={responseType.AssemblyName}");
                    object? typedResult = null;

                    if (frc.Result is System.Text.Json.JsonElement jsonElement)
                    {
                        Console.WriteLine($"[SUPERVISOR DEBUG] Result is JsonElement: {jsonElement.GetRawText()}");

                        // Try to get the type
                        var typeString = responseType.TypeName + ", " + responseType.AssemblyName;
                        var targetType = Type.GetType(typeString);
                        Console.WriteLine($"[SUPERVISOR DEBUG] Type.GetType('{typeString}') = {targetType?.FullName ?? "NULL"}");

                        if (targetType == null)
                        {
                            // Try without assembly name
                            targetType = Type.GetType(responseType.TypeName);
                            Console.WriteLine($"[SUPERVISOR DEBUG] Fallback Type.GetType('{responseType.TypeName}') = {targetType?.FullName ?? "NULL"}");
                        }

                        // Use Web defaults which includes camelCase property naming
                        typedResult = System.Text.Json.JsonSerializer.Deserialize(
                            jsonElement.GetRawText(),
                            targetType ?? typeof(object),
                            s_jsonOptions);
                        Console.WriteLine($"[SUPERVISOR DEBUG] Deserialized result type: {typedResult?.GetType().FullName ?? "NULL"}");
                    }
                    else
                    {
                        typedResult = frc.Result;
                        Console.WriteLine($"[SUPERVISOR DEBUG] Result is not JsonElement, using as-is: {typedResult?.GetType().FullName ?? "NULL"}");
                    }

                    // Dispatch based on the typed result
                    Console.WriteLine($"[SUPERVISOR DEBUG] Dispatching based on typedResult type: {typedResult?.GetType().FullName ?? "NULL"}");
                    switch (typedResult)
                    {
                        case Flight flight:
                            Console.WriteLine("[SUPERVISOR DEBUG] Matched Flight, calling HandleSelectedFlightAsync");
                            await HandleSelectedFlightAsync(flight, context, cancellationToken);
                            return; // Handler continued the flow
                        case Hotel hotel:
                            Console.WriteLine("[SUPERVISOR DEBUG] Matched Hotel, calling HandleSelectedHotelAsync");
                            await HandleSelectedHotelAsync(hotel, context, cancellationToken);
                            return; // Handler continued the flow
                        default:
                            Console.WriteLine("[SUPERVISOR DEBUG] No match for typedResult, falling through");
                            break;
                    }
                }
            }
        }

        // No pending results - proceed with normal routing based on state
        var state = await TravelAgentWorkflowFactory.GetStateAsync(context, cancellationToken);

        // Check if this is the initial run
        var hasInitialized = !string.IsNullOrEmpty(state.Origin) && !string.IsNullOrEmpty(state.Destination);
        if (!hasInitialized)
        {
            // Emit welcome message
            await context.AddEventAsync(new AgentResponseUpdateEvent(
                this.Id,
                new AgentResponseUpdate(ChatRole.Assistant, $"Noted your travel from {state.Origin} to {state.Destination}. Let me find some flight options for you!")),
                cancellationToken);
        }

        // Route based on what's missing
        if (state.Itinerary.SelectedFlight is null)
        {
            // Route to flights agent
            await context.SendMessageAsync(state, "FlightsExecutor", cancellationToken);
        }
        else if (state.Itinerary.SelectedHotel is null)
        {
            // Route to hotels agent
            await context.SendMessageAsync(state, "HotelsExecutor", cancellationToken);
        }
        else if (state.Itinerary.SelectedExperiences is null)
        {
            // Route to experiences agent
            await context.SendMessageAsync(state, "ExperiencesExecutor", cancellationToken);
        }
        else
        {
            // All done - emit completion summary
            var summary = $"""
                🎉 Your trip to {state.Destination} is all planned!

                ✈️ Flight: {state.Itinerary.SelectedFlight.Airline} - {state.Itinerary.SelectedFlight.Price}
                   {state.Itinerary.SelectedFlight.Departure} → {state.Itinerary.SelectedFlight.Arrival}

                🏨 Hotel: {state.Itinerary.SelectedHotel.Name}
                   {state.Itinerary.SelectedHotel.Location} - {state.Itinerary.SelectedHotel.PricePerNight}

                🎯 Experiences: {state.Itinerary.SelectedExperiences.Count} activities planned

                Have an amazing trip!
                """;

            await context.AddEventAsync(new AgentResponseUpdateEvent(
                this.Id,
                new AgentResponseUpdate(ChatRole.Assistant, summary)),
                cancellationToken);
        }
    }
}

/// <summary>
/// Executor that presents flight options and sends them to the request port for user selection.
/// </summary>
internal sealed class FlightsExecutor() : Executor<TravelAgentState>("FlightsExecutor")
{
    public override async ValueTask HandleAsync(TravelAgentState message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var flights = TravelData.Flights;

        // Update state with available flights
        var state = await TravelAgentWorkflowFactory.GetStateAsync(context, cancellationToken);
        state.Flights = [.. flights];
        await TravelAgentWorkflowFactory.UpdateStateAsync(context, state, cancellationToken);

        // Emit state snapshot so the UI can display the flights
        await TravelAgentWorkflowFactory.EmitStateSnapshotAsync(context, state, "flights", cancellationToken);

        // Create the selection request with options
        var request = new FlightSelectionRequest
        {
            Message = $"Found {flights.Length} flight options from {state.Origin} to {state.Destination}. I recommend the {flights[0].Airline} flight as it's cheaper and has good on-time performance.",
            Options = [.. flights],
            Recommendation = flights[0],
            Agent = "flights"
        };

        // Send to the request port - this will trigger an AG-UI interrupt
        await context.SendMessageAsync(request, cancellationToken: cancellationToken);
    }
}

/// <summary>
/// Executor that presents hotel options and sends them to the request port for user selection.
/// </summary>
internal sealed class HotelsExecutor() : Executor<TravelAgentState>("HotelsExecutor")
{
    public override async ValueTask HandleAsync(TravelAgentState message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var hotels = TravelData.Hotels;

        // Update state with available hotels
        var state = await TravelAgentWorkflowFactory.GetStateAsync(context, cancellationToken);
        state.Hotels = [.. hotels];
        await TravelAgentWorkflowFactory.UpdateStateAsync(context, state, cancellationToken);

        // Emit state snapshot so the UI can display the hotels
        await TravelAgentWorkflowFactory.EmitStateSnapshotAsync(context, state, "hotels", cancellationToken);

        // Create the hotel selection request
        var request = new HotelSelectionRequest
        {
            Message = $"Found {hotels.Length} accommodation options in {state.Destination}. I recommend {hotels[2].Name} as it offers the best balance of price, rating, and location.",
            Options = [.. hotels],
            Recommendation = hotels[2],
            Agent = "hotels"
        };

        // Send to the request port - this will trigger an AG-UI interrupt
        await context.SendMessageAsync(request, cancellationToken: cancellationToken);
    }
}

/// <summary>
/// Executor that finds experiences (restaurants and activities).
/// </summary>
internal sealed class ExperiencesExecutor() : Executor<TravelAgentState>("ExperiencesExecutor")
{
    public override async ValueTask HandleAsync(TravelAgentState message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var experiences = TravelData.Experiences;

        // Update state
        var state = await TravelAgentWorkflowFactory.GetStateAsync(context, cancellationToken);
        state.Experiences = [.. experiences];
        state.Itinerary.SelectedExperiences = [.. experiences];
        await TravelAgentWorkflowFactory.UpdateStateAsync(context, state, cancellationToken);

        // Emit state snapshot so the UI can display the experiences
        await TravelAgentWorkflowFactory.EmitStateSnapshotAsync(context, state, "experiences", cancellationToken);

        // Build recommendation message
        var activities = experiences.Where(e => e.Type == "activity").ToList();
        var restaurants = experiences.Where(e => e.Type == "restaurant").ToList();

        var experiencesMessage = $"Here are some great experiences for your trip to {state.Destination}:\n\n";
        experiencesMessage += "🎯 Activities:\n";
        foreach (var activity in activities)
        {
            experiencesMessage += $"  • {activity.Name} - {activity.Description} ({activity.Location})\n";
        }
        experiencesMessage += "\n🍽️ Restaurants:\n";
        foreach (var restaurant in restaurants)
        {
            experiencesMessage += $"  • {restaurant.Name} - {restaurant.Description} ({restaurant.Location})\n";
        }

        // Output the experiences message
        await context.AddEventAsync(new AgentResponseUpdateEvent(
            this.Id,
            new AgentResponseUpdate(ChatRole.Assistant, experiencesMessage)),
            cancellationToken);
    }
}
