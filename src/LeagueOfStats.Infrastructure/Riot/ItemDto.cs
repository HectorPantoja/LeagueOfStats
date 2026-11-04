using System.Text.Json;
using System.Text.Json.Serialization;
namespace LeagueOfStats.Infrastructure.Riot;

public sealed class ItemDto
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public string Plaintext { get; init; } = "";
    public required GoldDto Gold { get; init; }
    public required ImageDto Image { get; init; }
    public string[] From { get; init; } = [];
    public string[] Into { get; init; } = [];
    public string[] Tags { get; init; } = [];
    public Dictionary<string, bool> Maps { get; init; } = [];
    public Dictionary<string, double> Stats { get; init; } = [];
    public Dictionary<string, string> Effect { get; init; } = [];
    public bool Consumed { get; init; }
    public bool HideFromAll { get; init; }
    public bool? InStore { get; init; }
    public string? RequiredChampion { get; init; }
    public string? RequiredAlly { get; init; }
}
