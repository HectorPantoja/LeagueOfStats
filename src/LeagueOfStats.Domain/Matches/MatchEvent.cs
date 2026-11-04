namespace LeagueOfStats.Domain.Matches;

// Only normalized, identity-free fields cross the adapter boundary.
public sealed record MatchEvent(int EventId, double EventTime, string EventName,
    string? Participation = null, string? Objective = null, string? Result = null);
