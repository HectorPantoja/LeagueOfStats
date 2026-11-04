using LeagueOfStats.Domain.Catalog;
using LeagueOfStats.Domain.Matches;
namespace LeagueOfStats.Application;

public interface ILiveClient { Task<PlayerSample> ReadAsync(CancellationToken token); }
