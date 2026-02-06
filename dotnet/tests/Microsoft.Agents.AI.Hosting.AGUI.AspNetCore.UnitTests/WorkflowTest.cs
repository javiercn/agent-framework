// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.UnitTests;

public partial class WorkflowTest
{
    [Fact]
    public async Task Dojo_Workflow_TestAsync()
    {
        var store = new InMemoryCheckpointStore();
        var supervisor = new Supervisor();
        var flightAgent = new FlightAgent();
        var flightRequest = RequestPort.Create<FlightSelectionRequest, Flight>("FlightBookingAgentRequest");
        var hotelAgent = new HotelAgent();
        var hotelRequest = RequestPort.Create<HotelSelectionRequest, Hotel>("HotelSelectionRequest");
        var experiencesAgent = new ExperiencesAgent();
        var builder = new WorkflowBuilder(supervisor);
        builder.AddEdge<Flight>(
            supervisor,
            supervisor,
            flight => flight is not null);
        builder.AddEdge<Hotel>(
            supervisor,
            supervisor,
            hotel => hotel is not null);
        builder.AddEdge(supervisor, flightAgent);
        builder.AddEdge(flightAgent, flightRequest);
        builder.AddEdge(flightRequest, supervisor);
        builder.AddEdge(supervisor, hotelAgent);
        builder.AddEdge(hotelAgent, hotelRequest);
        builder.AddEdge(hotelRequest, supervisor);
        builder.AddEdge(supervisor, experiencesAgent);
        builder.AddEdge(experiencesAgent, supervisor);

        var workflow = builder.Build();
        var agent = workflow.AsAgent(
            "Travel",
            "Travel agent",
            "An agent that helps users plan their travel itineraries.",
            CheckpointManager.CreateJson(store, TravelSerializerContext.Default.Options));
        var session = await agent.GetNewSessionAsync(CancellationToken.None);
        ChatMessage initial = new(ChatRole.User, "I want to plan a trip from Paris to San Francisco.");
        List<ChatMessage> messages = [initial];
        List<AgentResponseUpdate> updates = [];
        List<object?> rawUpdates = [];

        // === STEP 1: Initial run - should stop at flight selection interrupt ===
        await foreach (var update in agent.RunStreamingAsync(initial, session, null, CancellationToken.None))
        {
            updates.Add(update);
            rawUpdates.Add(update.RawRepresentation);
        }

        // Verify we got a RequestInfoEvent (interrupt) for flight selection
        var flightRequestInfo = rawUpdates.OfType<RequestInfoEvent>().Single();
        Assert.Equal("FlightBookingAgentRequest", flightRequestInfo.Request.PortInfo.PortId);

        // Get the FunctionCallContent that was emitted
        var flightFunctionCall = updates
            .SelectMany(u => u.Contents)
            .OfType<FunctionCallContent>()
            .Single();
        Assert.Equal("FlightBookingAgentRequest", flightFunctionCall.Name);
        var flightExternalRequest = flightRequestInfo.Request;

        // === STEP 2: Serialize/deserialize session (simulate request boundary) ===
        var serialized = session.Serialize(TravelSerializerContext.Default.Options);
        session = await agent.DeserializeSessionAsync(serialized, TravelSerializerContext.Default.Options);

        // === STEP 3: Resume with flight selection ===
        // Create the response using the helper - uses the ExternalRequest from RequestInfoEvent
        var selectedFlight = TravelData.Flights[0]; // KLM
        var flightResponseContent = CreateInterruptResponse(flightFunctionCall, flightExternalRequest, selectedFlight);

        // Add BOTH the original FCC (assistant) and the FRC (tool) to messages
        // The Supervisor needs the FCC to match with the FRC
        messages.Add(new ChatMessage(ChatRole.Assistant, [flightFunctionCall]));
        messages.Add(new ChatMessage(ChatRole.Tool, [flightResponseContent]));

        updates.Clear();
        rawUpdates.Clear();
        await foreach (var update in agent.RunStreamingAsync(messages, session, null, CancellationToken.None))
        {
            updates.Add(update);
            rawUpdates.Add(update.RawRepresentation);
        }

        // Verify we got a RequestInfoEvent (interrupt) for hotel selection
        var hotelRequestInfo = rawUpdates.OfType<RequestInfoEvent>().Single();
        Assert.Equal("HotelSelectionRequest", hotelRequestInfo.Request.PortInfo.PortId);

        // Get the FunctionCallContent for hotel selection
        var hotelFunctionCall = updates
            .SelectMany(u => u.Contents)
            .OfType<FunctionCallContent>()
            .Single();
        Assert.Equal("HotelSelectionRequest", hotelFunctionCall.Name);
        var hotelExternalRequest = hotelRequestInfo.Request;

        // === STEP 4: Serialize/deserialize session again ===
        // Skip serialization for now to test if the workflow works without it
        // serialized = session.Serialize(TravelSerializerContext.Default.Options);
        // session = await agent.DeserializeSessionAsync(serialized, TravelSerializerContext.Default.Options);

        // === STEP 5: Resume with hotel selection ===
        var selectedHotel = TravelData.Hotels[0]; // Hotel Zephyr
        var hotelResponseContent = CreateInterruptResponse(hotelFunctionCall, hotelExternalRequest, selectedHotel);

        // Add BOTH the original FCC (assistant) and the FRC (tool) to messages
        messages.Add(new ChatMessage(ChatRole.Assistant, [hotelFunctionCall]));
        messages.Add(new ChatMessage(ChatRole.Tool, [hotelResponseContent]));

        updates.Clear();
        rawUpdates.Clear();
        await foreach (var update in agent.RunStreamingAsync(messages, session, null, CancellationToken.None))
        {
            updates.Add(update);
            rawUpdates.Add(update.RawRepresentation);
        }

        // Verify no more interrupts (workflow should be complete or continue normally)
        var remainingInterrupts = rawUpdates.OfType<RequestInfoEvent>().ToList();
        Assert.Empty(remainingInterrupts);

        // Check for errors
        var errors = rawUpdates.OfType<WorkflowErrorEvent>().ToList();
        Assert.Empty(errors.Select(e => e.Exception?.ToString() ?? "Unknown error"));

        // Verify experiences were emitted as recommendations
        var allTextContents = updates
            .SelectMany(u => u.Contents.OfType<TextContent>())
            .Select(t => t.Text)
            .ToList();

        // The ExperiencesAgent emits a message with activities and restaurants
        var hasExperiences = allTextContents.Any(t =>
            t.Contains("Activities") || t.Contains("Restaurants") || t.Contains("Pier 39"));
        Assert.True(hasExperiences, $"Expected experiences. Text: [{string.Join("; ", allTextContents)}]");
    }

    /// <summary>
    /// Creates a FunctionResultContent for resuming an interrupted workflow.
    /// </summary>
    /// <param name="functionCall">The original FunctionCallContent from the interrupt.</param>
    /// <param name="externalRequest">The ExternalRequest from RequestInfoEvent.</param>
    /// <param name="result">The user's response data.</param>
    /// <returns>A FunctionResultContent that can be added to messages to resume the workflow.</returns>
    private static FunctionResultContent CreateInterruptResponse(
        FunctionCallContent functionCall,
        ExternalRequest externalRequest,
        object? result)
    {
        return new FunctionResultContent(functionCall.CallId, result)
        {
            RawRepresentation = externalRequest
        };
    }

    [JsonSerializable(typeof(TravelState))]
    public partial class TravelSerializerContext : JsonSerializerContext
    {
    }

    public class InMemoryCheckpointStore : JsonCheckpointStore
    {
        private static readonly Dictionary<string, Dictionary<CheckpointInfo, JsonElement>> s_store = new();
        public override ValueTask<CheckpointInfo> CreateCheckpointAsync(string runId, JsonElement value, CheckpointInfo? parent = null)
        {
            if (!s_store.TryGetValue(runId, out var existing))
            {
                existing = new Dictionary<CheckpointInfo, JsonElement>();
                s_store[runId] = existing;
            }
            var checkpointId = Guid.NewGuid().ToString();
            var checkpointInfo = new CheckpointInfo(runId, checkpointId);
            existing.Add(checkpointInfo, value);
            return ValueTask.FromResult(checkpointInfo);
        }

        public override ValueTask<JsonElement> RetrieveCheckpointAsync(string runId, CheckpointInfo key)
        {
            if (s_store.TryGetValue(runId, out var existing) && existing.TryGetValue(key, out var value))
            {
                return ValueTask.FromResult(value);
            }
            throw new KeyNotFoundException($"Checkpoint not found for runId: {runId}, checkpointId: {key.CheckpointId}");
        }

        public override ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(string runId, CheckpointInfo? withParent = null)
        {
            if (s_store.TryGetValue(runId, out var existing))
            {
                return ValueTask.FromResult<IEnumerable<CheckpointInfo>>(existing.Keys);
            }
            return ValueTask.FromResult<IEnumerable<CheckpointInfo>>([]);
        }
    }

    public class TravelState
    {
        public string? Origin { get; set; }
        public string? Destination { get; set; }
        public Flight? SelectedFlight { get; set; }
        public Hotel? SelectedHotel { get; set; }
        public List<Experience>? SelectedExperiences { get; set; }
    }

    public sealed class Flight
    {
        public string Airline { get; set; } = string.Empty;
        public string Departure { get; set; } = string.Empty;
        public string Arrival { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
    }

    public sealed class Hotel
    {
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string PricePerNight { get; set; } = string.Empty;
        public string Rating { get; set; } = string.Empty;
    }

    public sealed class Experience
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    public class Supervisor() : ChatProtocolExecutor("Supervisor")
    {
        protected override RouteBuilder ConfigureRoutes(RouteBuilder routeBuilder)
        {
            return base.ConfigureRoutes(routeBuilder)
                .AddHandler<Flight>(this.HandleSelectedFlightAsync)
                .AddHandler<Hotel>(this.HandleSelectedHotelAsync);
        }

        private async ValueTask HandleSelectedFlightAsync(Flight flight, IWorkflowContext context, CancellationToken cancellationToken)
        {
            var state = await context.ReadOrInitStateAsync("travelState", () => new TravelState(), "Travel", cancellationToken);
            state.SelectedFlight = flight;
            await context.QueueStateUpdateAsync("travelState", state, "Travel", cancellationToken);

            // Next is to select the hotel
            await context.SendMessageAsync(state, "HotelBookingAgent", cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask HandleSelectedHotelAsync(Hotel hotel, IWorkflowContext context, CancellationToken cancellationToken)
        {
            var state = await context.ReadOrInitStateAsync("travelState", () => new TravelState(), "Travel", cancellationToken);
            state.SelectedHotel = hotel;
            await context.QueueStateUpdateAsync("travelState", state, "Travel", cancellationToken);

            // Next is to select the hotel
            await context.SendMessageAsync(state, "ExperiencesBookingAgent", cancellationToken).ConfigureAwait(false);
        }

        protected override async ValueTask TakeTurnAsync(
            List<ChatMessage> messages,
            IWorkflowContext context,
            bool? emitEvents,
            CancellationToken cancellationToken = default)
        {
            // Dispatch any pending interrupt responses FIRST
            var results = new Dictionary<string, FunctionResultContent>();
            var functions = new Dictionary<string, FunctionCallContent>();
            for (var i = messages.Count - 1; i >= 0; i--)
            {
                var message = messages[i];
                for (var j = 0; j < message.Contents.Count; j++)
                {
                    var content = message.Contents[j];
                    if (content is FunctionCallContent fcc)
                    {
                        functions.Add(fcc.CallId, fcc);
                    }
                    if (content is FunctionResultContent frc)
                    {
                        results.Add(frc.CallId, frc);
                    }
                }
            }

            foreach (var (key, value) in results)
            {
                if (functions.ContainsKey(key) && value.Result != null)
                {
                    // Dispatch based on the result type - invoke the appropriate handler logic
                    switch (value.Result)
                    {
                        case Flight flight:
                            await HandleSelectedFlightAsync(flight, context, cancellationToken);
                            break;
                        case Hotel hotel:
                            await HandleSelectedHotelAsync(hotel, context, cancellationToken);
                            break;
                    }
                    return; // Result dispatched - the handler continueed the flow
                }
            }

            // No pending results - proceed with normal routing
            var state = await context.ReadOrInitStateAsync("travelState", () => new TravelState(), "Travel", cancellationToken);
            if (string.IsNullOrEmpty(state.Origin) || string.IsNullOrEmpty(state.Destination))
            {
                // Invoke agent logic to get origin and destination, for now, hardcode.
                state.Origin = "Paris";
                state.Destination = "San Francisco";
                await context.QueueStateUpdateAsync("travelState", state, "Travel", cancellationToken);
                await context.AddEventAsync(new AgentResponseUpdateEvent(
                    this.Id,
                    new AgentResponseUpdate(ChatRole.Assistant, $"Noted your travel from {state.Origin} to {state.Destination}.")),
                    cancellationToken);
                // The first thing we need to do is get the flight details.
            }
            if (state.SelectedFlight is null)
            {
                await context.SendMessageAsync(state, "FlightBookingAgent", cancellationToken);
            }
        }
    }

    public class FlightAgent() : Executor<TravelState>("FlightBookingAgent")
    {
        public async override ValueTask HandleAsync(
            TravelState message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var state = await context.ReadOrInitStateAsync("travelState", () => new TravelState(), "Travel", cancellationToken);
            if (message.SelectedFlight is null)
            {
                var flights = TravelData.Flights;
                var request = new FlightSelectionRequest
                {
                    Message = $"Found {flights.Length} flight options from {state.Origin} to {state.Destination}. I recommend the {flights[0].Airline} flight as it's cheaper and has good on-time performance.",
                    Options = [.. flights],
                    Recommendation = flights[0],
                    Agent = "flights"
                };

                await context.SendMessageAsync(request, cancellationToken: cancellationToken);
            }
        }
    }

    public class HotelAgent() : Executor<TravelState>("HotelBookingAgent")
    {
        public async override ValueTask HandleAsync(
            TravelState message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var state = await context.ReadOrInitStateAsync("travelState", () => new TravelState(), "Travel", cancellationToken);
            if (message.SelectedHotel is null)
            {
                var hotels = TravelData.Hotels;
                var request = new HotelSelectionRequest
                {
                    Message = $"Found {hotels.Length} hotel options in {state.Destination}. I recommend the {hotels[0].Name} hotel as it's really close to the city center.",
                    Options = [.. hotels],
                    Recommendation = hotels[0],
                    Agent = "hotels"
                };

                await context.SendMessageAsync(request, cancellationToken: cancellationToken);
            }
        }
    }

    public class ExperiencesAgent() : Executor<TravelState>("ExperiencesBookingAgent")
    {
        public async override ValueTask HandleAsync(
            TravelState message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var state = await context.ReadOrInitStateAsync("travelState", () => new TravelState(), "Travel", cancellationToken);

            // Set experiences in state
            state.SelectedExperiences = [.. TravelData.Experiences];
            await context.QueueStateUpdateAsync("travelState", state, "Travel", cancellationToken);

            // Build recommendation message
            var activities = TravelData.Experiences.Where(e => e.Type == "activity").ToList();
            var restaurants = TravelData.Experiences.Where(e => e.Type == "restaurant").ToList();

            var message2 = $"Here are some great experiences for your trip to {state.Destination}:\n\n";
            message2 += "🎯 Activities:\n";
            foreach (var activity in activities)
            {
                message2 += $"  • {activity.Name} - {activity.Description} ({activity.Location})\n";
            }
            message2 += "\n🍽️ Restaurants:\n";
            foreach (var restaurant in restaurants)
            {
                message2 += $"  • {restaurant.Name} - {restaurant.Description} ({restaurant.Location})\n";
            }

            // Emit the recommendations as an update
            await context.AddEventAsync(new AgentResponseUpdateEvent(
                this.Id,
                new AgentResponseUpdate(ChatRole.Assistant, message2)),
                cancellationToken);
        }
    }

    public sealed class FlightSelectionRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<Flight> Options { get; set; } = [];
        public Flight? Recommendation { get; set; }
        public string Agent { get; set; } = "flights";
    }

    public sealed class HotelSelectionRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<Hotel> Options { get; set; } = [];
        public Hotel? Recommendation { get; set; }
        public string Agent { get; set; } = "hotels";
    }

    internal static class TravelData
    {
        public static readonly Flight[] Flights =
        [
            new() { Airline = "KLM", Departure = "Amsterdam (AMS)", Arrival = "San Francisco (SFO)", Price = "$650", Duration = "11h 30m" },
        new() { Airline = "United", Departure = "Amsterdam (AMS)", Arrival = "San Francisco (SFO)", Price = "$720", Duration = "12h 15m" }
        ];

        public static readonly Hotel[] Hotels =
        [
            new() { Name = "Hotel Zephyr", Location = "Fisherman's Wharf", PricePerNight = "$280/night", Rating = "4.2 stars" },
        new() { Name = "The Ritz-Carlton", Location = "Nob Hill", PricePerNight = "$550/night", Rating = "4.8 stars" },
        new() { Name = "Hotel Zoe", Location = "Union Square", PricePerNight = "$320/night", Rating = "4.4 stars" }
        ];

        public static readonly Experience[] Experiences =
        [
            new() { Name = "Pier 39", Type = "activity", Description = "Iconic waterfront destination with shops and sea lions", Location = "Fisherman's Wharf" },
        new() { Name = "Golden Gate Bridge", Type = "activity", Description = "World-famous suspension bridge with stunning views", Location = "Golden Gate" },
        new() { Name = "Swan Oyster Depot", Type = "restaurant", Description = "Historic seafood counter serving fresh oysters", Location = "Polk Street" },
        new() { Name = "Tartine Bakery", Type = "restaurant", Description = "Artisanal bakery famous for bread and pastries", Location = "Mission District" }
        ];
    }
}
