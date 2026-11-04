using System.Text.Json;
using System.Text.Json.Serialization;
namespace LeagueOfStats.Infrastructure.Riot;

public sealed class ChampionDto
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string Title { get; init; } = "";
    public string Blurb { get; init; } = "";
    public string Partype { get; init; } = "";
    public string[] Tags { get; init; } = [];
    public Dictionary<string, double> Stats { get; init; } = [];
    public Dictionary<string, int> Info { get; init; } = [];
    public required ImageDto Image { get; init; }
    // Detail endpoint only; summary files omit these fields.
    public string? Lore { get; init; }
    public string[] Allytips { get; init; } = [];
    public string[] Enemytips { get; init; } = [];
    public SpellDto[] Spells { get; init; } = [];
    public PassiveDto? Passive { get; init; }
}
