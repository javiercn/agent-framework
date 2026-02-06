// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoServer.Subgraphs;

/// <summary>
/// Static data for the travel agent demo (same as LangGraph example).
/// </summary>
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
