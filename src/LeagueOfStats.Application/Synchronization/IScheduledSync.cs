namespace LeagueOfStats.Application.Synchronization;

public interface IScheduledSync
{
    Task<string> ExecuteAsync(bool force, CancellationToken token);
}
