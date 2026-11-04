namespace LeagueOfStats.Domain.Synchronization;

public sealed record SyncState(DateTimeOffset? LastAttemptUtc, DateTimeOffset? LastSuccessUtc, string? Summary);
