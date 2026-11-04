using System.Text.Json;
using System.Text.Json.Serialization;
namespace LeagueOfStats.Infrastructure.Riot;

public sealed record GoldDto(int Base, int Total, int Sell, bool Purchasable);
