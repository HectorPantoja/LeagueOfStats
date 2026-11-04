namespace LeagueOfStats.Domain.Catalog;

public sealed record Item(int Id, string Name, string Description, string Plaintext, ItemGold Gold,
    IReadOnlyList<string> From, IReadOnlyList<string> Into, IReadOnlyList<string> Tags, IReadOnlyDictionary<string, bool> Maps,
    IReadOnlyDictionary<string, double> Stats, ImageAsset Image);

