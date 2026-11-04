using System.Text.Json;
using LeagueOfStats.Domain.Matches;
namespace LeagueOfStats.Infrastructure.Riot;

public static class LiveEventParser
{
    public static readonly IReadOnlySet<string> AllowedNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "GameStart", "GameEnd", "ChampionKill", "DragonKill", "BaronKill", "HeraldKill",
        "HordeKill", "AtakhanKill", "TurretKilled", "InhibKilled"
    };

    public static IReadOnlyList<MatchEvent> Parse(JsonElement root, string activeName)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Events", out var list) ||
            list.ValueKind != JsonValueKind.Array) throw new JsonException("Invalid events envelope.");
        var result = new List<MatchEvent>();
        foreach (var e in list.EnumerateArray())
        {
            var name = Text(e, "EventName");
            if (name is null || !AllowedNames.Contains(name)) continue;
            if (
                !e.TryGetProperty("EventID", out var id) || id.ValueKind != JsonValueKind.Number ||
                !id.TryGetInt32(out var eventId) || eventId < 0 ||
                !e.TryGetProperty("EventTime", out var time) || time.ValueKind != JsonValueKind.Number ||
                !time.TryGetDouble(out var seconds) || !double.IsFinite(seconds) || seconds < 0)
                throw new JsonException("Invalid event identity or timestamp.");

            var participation = Text(e, "KillerName") == activeName ? "Kill" :
                Text(e, "VictimName") == activeName ? "Death" :
                e.TryGetProperty("Assisters", out var a) && a.ValueKind == JsonValueKind.Array &&
                a.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == activeName)
                    ? "Assist" : null;
            // Only our own kill/death/assist events. No rival identities or inferred fight results.
            if (name == "ChampionKill" && participation is null) continue;
            var dragon = Text(e, "DragonType");
            var objective = name == "DragonKill" && dragon is "Air" or "Earth" or "Fire" or "Water" or "Hextech" or "Chemtech" or "Elder"
                ? dragon : null;
            var gameResult = Text(e, "Result");
            result.Add(new(eventId, seconds, name, participation, objective,
                name == "GameEnd" && gameResult is "Win" or "Lose" ? gameResult : null));
        }
        return result.AsReadOnly();
    }

    private static string? Text(JsonElement e, string property) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}

