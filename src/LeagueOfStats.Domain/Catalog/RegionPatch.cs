using System.Text.RegularExpressions;

namespace LeagueOfStats.Domain.Catalog;

public sealed record RegionPatch
{
    public string Region { get; }
    public PatchVersion Version { get; }
    public string Locale { get; }

    public RegionPatch(string region, PatchVersion version, string locale)
    {
        if (region is null || !Regex.IsMatch(region, "\\A[a-z]{2,5}\\z"))
            throw new ArgumentException("Invalid region", nameof(region));
        if (locale is null || !Regex.IsMatch(locale, "\\A[a-z]{2}_[A-Z]{2}\\z"))
            throw new ArgumentException("Invalid locale", nameof(locale));
        ArgumentNullException.ThrowIfNull(version);
        Region = region;
        Version = version;
        Locale = locale;
    }
}
