namespace LeagueOfStats.Domain.Catalog;

public sealed record Champion(string Id, int Key, string Name, string Title, string Blurb,
    string ResourceType, IReadOnlyList<string> Tags, IReadOnlyDictionary<string, double> Stats, ImageAsset Image);

