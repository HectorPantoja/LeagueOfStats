namespace LeagueOfStats.Domain.Matches;

public sealed record MatchSummary(string SessionId, string StartedUtc, string? EndedUtc, string Status,
    string GameMode, int MapNumber, string Champion, string Position, double DurationSeconds,
    double FirstGameTime, bool HasGaps, bool EventsIncomplete, string Result, ReviewPoint? Final, string ChampionId = "")
{
    public string Label => $"{StartedUtc} · {GameMode} / mapa {MapNumber} · {Champion} · {Status}";
    public bool CanReview => Status is "Completed" or "Interrupted" or "Legacy";
    public bool Comparable => Status == "Completed" && FirstGameTime <= 60 && !HasGaps &&
        !EventsIncomplete && DurationSeconds >= 300 && GameMode != "UNKNOWN" && MapNumber > 0 && Final is not null;
    public double? CsPerMinute => Final is null || DurationSeconds <= 0 ? null : Final.CreepScore * 60 / DurationSeconds;
    public double? DeathsPerTenMinutes => Final is null || DurationSeconds <= 0 ? null : Final.Deaths * 600 / DurationSeconds;
}
