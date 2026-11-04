namespace LeagueOfStats.Domain.Synchronization;

public interface ISyncSchedule
{
    bool IsDue(DateTimeOffset now, DateTimeOffset? anchor, SyncState state);
}
