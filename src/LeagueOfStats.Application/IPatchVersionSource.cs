using LeagueOfStats.Domain.Catalog;
namespace LeagueOfStats.Application;

public interface IPatchVersionSource
{
    Task<IReadOnlyList<PatchVersion>> GetVersionsAsync(CancellationToken token);
}
