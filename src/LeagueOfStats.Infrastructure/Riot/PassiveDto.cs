using System.Text.Json;
using System.Text.Json.Serialization;
namespace LeagueOfStats.Infrastructure.Riot;

public sealed record PassiveDto(string Name, string Description, ImageDto Image);
