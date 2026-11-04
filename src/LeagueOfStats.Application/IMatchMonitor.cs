using LeagueOfStats.Domain.Matches;
namespace LeagueOfStats.Application;

public interface IMatchMonitor
{
    Task<PlayerSample> CaptureAsync(CancellationToken token);
    Task ReportUnavailableAsync(CancellationToken token);
}
