// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;
using AGUIDojoServer.AgenticUI;
using AGUIDojoServer.BackendToolRendering;
using AGUIDojoServer.PredictiveStateUpdates;
using AGUIDojoServer.SharedState;
using AGUIDojoServer.Subgraphs;

namespace AGUIDojoServer;

[JsonSerializable(typeof(WeatherInfo))]
[JsonSerializable(typeof(Recipe))]
[JsonSerializable(typeof(Ingredient))]
[JsonSerializable(typeof(RecipeResponse))]
[JsonSerializable(typeof(Plan))]
[JsonSerializable(typeof(Step))]
[JsonSerializable(typeof(StepStatus))]
[JsonSerializable(typeof(StepStatus?))]
[JsonSerializable(typeof(JsonPatchOperation))]
[JsonSerializable(typeof(List<JsonPatchOperation>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(DocumentState))]
// Subgraphs (Travel Agent) types
[JsonSerializable(typeof(Flight))]
[JsonSerializable(typeof(Flight[]))]
[JsonSerializable(typeof(List<Flight>))]
[JsonSerializable(typeof(Hotel))]
[JsonSerializable(typeof(Hotel[]))]
[JsonSerializable(typeof(List<Hotel>))]
[JsonSerializable(typeof(Experience))]
[JsonSerializable(typeof(Experience[]))]
[JsonSerializable(typeof(TravelItinerary))]
[JsonSerializable(typeof(TravelAgentState))]
[JsonSerializable(typeof(FlightSelectionRequest))]
[JsonSerializable(typeof(HotelSelectionRequest))]
[JsonSerializable(typeof(SupervisorResponse))]
[JsonSerializable(typeof(List<Experience>))]
internal sealed partial class AGUIDojoServerSerializerContext : JsonSerializerContext;
