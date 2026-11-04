using LeagueOfStats.Domain.Catalog;
using LeagueOfStats.Domain.Matches;
namespace LeagueOfStats.Application;

public interface ICatalogRepository
{
    Task<GameCatalog?> FindAsync(string region, string locale, CancellationToken token);
    Task SaveAsync(GameCatalog catalog, CancellationToken token);
}
