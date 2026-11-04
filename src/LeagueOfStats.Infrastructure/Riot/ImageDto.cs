using System.Text.Json;
using System.Text.Json.Serialization;
namespace LeagueOfStats.Infrastructure.Riot;

public sealed record ImageDto(string Full, string Sprite, string Group, int X, int Y, int W, int H);
