using System.Text.Json;
using LeagueOfStats.Domain.Matches;

namespace LeagueOfStats.Infrastructure.Riot;

public static class LiveTeamParser
{
    public static ActiveTeamSnapshot Parse(JsonElement active, JsonElement roster)
    {
        if (active.ValueKind != JsonValueKind.Object || roster.ValueKind != JsonValueKind.Array)
            throw new JsonException("Respuesta del jugador o equipo inválida.");

        var entries = roster.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.Object).ToArray();
        var riotId = Identity(active);
        var matches = string.IsNullOrWhiteSpace(riotId)
            ? Array.Empty<JsonElement>()
            : entries.Where(p => Identity(p) == riotId).ToArray();

        // Legacy clients may expose only summonerName. Never guess from champion or list order.
        if (matches.Length == 0 && Text(active, "summonerName") is { Length: > 0 } legacy)
            matches = entries.Where(p => Text(p, "summonerName") == legacy).ToArray();
        if (matches.Length != 1)
            throw new JsonException("No se pudo identificar de forma única al jugador activo; esperando datos consistentes.");

        var own = matches[0];
        if (!own.TryGetProperty("scores", out var ownScores) || ownScores.ValueKind != JsonValueKind.Object ||
            new[] { "kills", "deaths", "assists", "creepScore" }.Any(key =>
                !ownScores.TryGetProperty(key, out var score) || score.ValueKind != JsonValueKind.Number ||
                !score.TryGetInt32(out var number) || number < 0))
            throw new JsonException("Estadísticas propias incompletas; no se persistirán ceros inventados.");
        var team = Text(own, "team");
        if (string.IsNullOrWhiteSpace(team)) throw new JsonException("El jugador activo no tiene equipo identificado.");

        // Opponent payloads are discarded here, before creating domain objects or exposing data.
        var allies = entries.Where(p => Text(p, "team") == team).Select(ReadPlayer).ToArray();
        var activePlayer = ReadPlayer(own);
        var stats = active.TryGetProperty("championStats", out var championStats) ? championStats : default;
        return new ActiveTeamSnapshot(activePlayer, allies, Number(stats, "currentHealth"), Number(stats, "maxHealth"));
    }

    private static TeamPlayer ReadPlayer(JsonElement player)
    {
        var name = Identity(player) ?? Text(player, "summonerName");
        var champion = Text(player, "championName");
        var team = Text(player, "team");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(champion) || string.IsNullOrWhiteSpace(team))
            throw new JsonException("Datos de identidad incompletos en el equipo aliado.");
        var scores = player.TryGetProperty("scores", out var value) ? value : default;
        var items = new List<LiveItem>();
        if (player.TryGetProperty("items", out var inventory) && inventory.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in inventory.EnumerateArray())
            {
                var id = Integer(item, "itemID");
                if (id > 0) items.Add(new(id, Text(item, "displayName") ?? $"Objeto {id}", Integer(item, "count", 1)));
            }
        }
        return new TeamPlayer(name, champion, team, Text(player, "position") ?? "", Integer(player, "level", 1),
            Integer(scores, "kills"), Integer(scores, "deaths"), Integer(scores, "assists"), Integer(scores, "creepScore"),
            player.TryGetProperty("isDead", out var dead) && dead.ValueKind == JsonValueKind.True, Array.AsReadOnly(items.ToArray()))
        { WardScore = Number(scores, "wardScore"), ChampionId = ChampionId(player) };
    }

    private static string ChampionId(JsonElement player)
    {
        const string prefix = "game_character_displayname_";
        var raw = Text(player, "rawChampionName");
        return raw is not null && raw.StartsWith(prefix, StringComparison.Ordinal) ? raw[prefix.Length..] : "";
    }

    private static string? Identity(JsonElement player)
    {
        if (Text(player, "riotId") is { Length: > 0 } id) return id;
        var name = Text(player, "riotIdGameName");
        var tag = Text(player, "riotIdTagLine");
        return !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(tag) ? $"{name}#{tag}" : null;
    }

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static int Integer(JsonElement element, string property, int fallback = 0) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) && result >= 0 ? result : fallback;

    private static double? Number(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var result) && double.IsFinite(result) && result >= 0 ? result : null;
}
