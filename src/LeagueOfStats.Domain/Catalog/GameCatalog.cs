using System.Collections.Frozen;

namespace LeagueOfStats.Domain.Catalog;

// Aggregate boundary: a localized catalog is replaced as a whole, never partially.
public sealed class GameCatalog
{
    public RegionPatch Patch { get; }
    public IReadOnlyList<Champion> Champions { get; }
    public IReadOnlyList<Item> Items { get; }
    public GameCatalog(RegionPatch patch, IEnumerable<Champion> champions, IEnumerable<Item> items)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(champions);
        ArgumentNullException.ThrowIfNull(items);
        var cs = champions.ToArray(); var its = items.ToArray();
        if (cs.Length == 0 || its.Length == 0) throw new ArgumentException("Catalog must contain champions and items");
        if (cs.Any(c => c is null || string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Name) || c.Key <= 0) || its.Any(i => i is null || i.Id <= 0 || string.IsNullOrWhiteSpace(i.Name)) ||
            cs.Select(c => c.Id).Distinct().Count() != cs.Length || its.Select(i => i.Id).Distinct().Count() != its.Length)
            throw new ArgumentException("Invalid or duplicate catalog identity");
        Patch = patch;
        Champions = Array.AsReadOnly(cs.Select(c => c with
        {
            Tags = Array.AsReadOnly(c.Tags.ToArray()),
            Stats = c.Stats.ToFrozenDictionary()
        }).ToArray());
        Items = Array.AsReadOnly(its.Select(i => i with
        {
            From = Array.AsReadOnly(i.From.ToArray()),
            Into = Array.AsReadOnly(i.Into.ToArray()),
            Tags = Array.AsReadOnly(i.Tags.ToArray()),
            Maps = i.Maps.ToFrozenDictionary(),
            Stats = i.Stats.ToFrozenDictionary()
        }).ToArray());
    }
}

