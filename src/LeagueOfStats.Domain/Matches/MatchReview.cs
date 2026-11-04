namespace LeagueOfStats.Domain.Matches;

public sealed record MatchReview(MatchSummary Match, IReadOnlyList<ReviewPoint> Timeline, IReadOnlyList<MatchEvent> Events);
