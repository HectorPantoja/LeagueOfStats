using LeagueOfStats.Domain.Catalog;
using LeagueOfStats.Domain.Matches;
namespace LeagueOfStats.Application;

public sealed class RefreshCatalog(IRiotCatalogClient source, ICatalogRepository repository)
{
    public async Task<RegionPatch> ExecuteAsync(string region, string locale, CancellationToken token)
    {
        var patch = await source.GetRegionPatchAsync(region, locale, token);
        var existing = await repository.FindAsync(region, locale, token);
        if (existing?.Patch.Version != patch.Version)
            await repository.SaveAsync(await source.DownloadAsync(patch, token), token);
        return patch;
    }
}
