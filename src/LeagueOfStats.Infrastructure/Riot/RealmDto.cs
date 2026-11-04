using System.Text.Json;
using System.Text.Json.Serialization;
namespace LeagueOfStats.Infrastructure.Riot;

public sealed class RealmDto
{
    [JsonPropertyName("v")] public required string Version { get; init; }
    [JsonPropertyName("l")] public string? Locale { get; init; }
    [JsonPropertyName("n")] public Dictionary<string, string> ComponentVersions { get; init; } = [];
    public string? Cdn { get; init; }
}
