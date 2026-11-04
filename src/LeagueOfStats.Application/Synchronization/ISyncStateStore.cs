using LeagueOfStats.Domain.Synchronization;
namespace LeagueOfStats.Application.Synchronization;

public interface ISyncStateStore
{
    Task<DateTimeOffset?> GetAnchorAsync(CancellationToken token);
    Task<SyncState> GetAsync(string flow, CancellationToken token);
    Task AttemptAsync(string flow, DateTimeOffset now, CancellationToken token);
    Task CompleteAsync(string flow, DateTimeOffset now, string summary, CancellationToken token);
}
