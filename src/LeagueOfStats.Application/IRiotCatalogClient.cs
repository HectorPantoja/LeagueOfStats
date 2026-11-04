using LeagueOfStats.Domain.Catalog;
using LeagueOfStats.Domain.Matches;
namespace LeagueOfStats.Application;

public interface IRiotCatalogClient
{
    Task<RegionPatch> GetRegionPatchAsync(string region, string locale, CancellationToken token);
    Task<GameCatalog> DownloadAsync(RegionPatch patch, CancellationToken token);
}

