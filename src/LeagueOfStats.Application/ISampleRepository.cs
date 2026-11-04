using LeagueOfStats.Domain.Catalog;
using LeagueOfStats.Domain.Matches;
namespace LeagueOfStats.Application;

public interface ISampleRepository
{
    Task<RecordingState> RecordAsync(PlayerSample sample, DateTimeOffset now, CancellationToken token);
    Task MarkUnavailableAsync(DateTimeOffset now, CancellationToken token);
}
