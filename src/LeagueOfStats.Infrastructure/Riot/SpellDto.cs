using System.Text.Json;
using System.Text.Json.Serialization;
namespace LeagueOfStats.Infrastructure.Riot;

public sealed class SpellDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public string Tooltip { get; init; } = "";
    public string Resource { get; init; } = "";
    public int Maxrank { get; init; }
    public double[] Cooldown { get; init; } = [];
    public double[] Cost { get; init; } = [];
    public double[] Range { get; init; } = [];
    public double[]?[] Effect { get; init; } = [];
    public string?[] EffectBurn { get; init; } = [];
    public ImageDto? Image { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalFields { get; init; }
}
