namespace LeagueOfStats.Domain.Synchronization;

public sealed class FortnightlySchedule(TimeZoneInfo zone) : ISyncSchedule
{
    public bool IsDue(DateTimeOffset now, DateTimeOffset? anchor, SyncState state)
    {
        // Persisted retry delay also applies across restarts.
        if (state.LastAttemptUtc is { } attempt && now - attempt < TimeSpan.FromHours(1)) return false;
        if (anchor is null || state.LastSuccessUtc is null) return true;
        var start = LocalDate(anchor.Value);
        var today = LocalDate(now);
        var monday = start.AddDays(-(((int)start.DayOfWeek + 6) % 7));
        var lastSuccess = LocalDate(state.LastSuccessUtc.Value);
        // Only the latest missed slot is needed; no replay of historical downloads.
        for (var day = today; day > lastSuccess && day >= start; day = day.AddDays(-1))
        {
            var week = (day.DayNumber - monday.DayNumber) / 7;
            if (week % 2 == 0 && day.DayOfWeek is DayOfWeek.Wednesday or DayOfWeek.Thursday)
                return true;
        }
        return false;
    }

    private DateOnly LocalDate(DateTimeOffset value) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, zone).DateTime);
}

