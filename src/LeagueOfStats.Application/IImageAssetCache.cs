namespace LeagueOfStats.Application;

public interface IImageAssetCache
{
    // Returns a local file URI, or null when the official image could not be loaded.
    Task<string?> GetAsync(string officialUrl, CancellationToken token);
}
