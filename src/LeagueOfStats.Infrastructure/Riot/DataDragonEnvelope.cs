using System.Text.Json;
using System.Text.Json.Serialization;
namespace LeagueOfStats.Infrastructure.Riot;

public sealed class DataDragonEnvelope<T>
{
    public required string Version { get; init; }
    public required Dictionary<string, T> Data { get; init; }
}
