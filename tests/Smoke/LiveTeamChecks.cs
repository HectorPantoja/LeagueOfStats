using System.Net;
using System.Text.Json;
using LeagueOfStats.Infrastructure.Riot;

internal static class LiveTeamChecks
{
    private const string Active = """
        {"riotId":"Player#LAN","summonerName":"Player","level":8,"currentGold":1200,
         "championStats":{"currentHealth":750,"maxHealth":1300}}
        """;
    private const string Roster = """
        [
          {"riotId":"Player#LAN","summonerName":"Player","championName":"Ahri","team":"ORDER","level":8,
           "position":"MIDDLE","isDead":false,"scores":{"kills":3,"deaths":1,"assists":5,"creepScore":90},
           "items":[{"itemID":1001,"displayName":"Boots","count":1}]},
          {"riotId":"Ally#LAN","championName":"Leona","team":"ORDER","level":7,
           "isDead":true,"scores":{"kills":1,"deaths":2,"assists":8,"creepScore":20},"items":[]},
          {"riotId":"Enemy#LAN","championName":"Ahri","team":"CHAOS","scores":"not parsed"}
        ]
        """;

    public static async Task RunAsync()
    {
        using var player = JsonDocument.Parse(Active);
        using var roster = JsonDocument.Parse(Roster);
        var snapshot = LiveTeamParser.Parse(player.RootElement, roster.RootElement);
        Check(snapshot.ActivePlayer.Name == "Player#LAN", "Wrong active player");
        Check(snapshot.Players.Count == 2 && snapshot.Players.All(p => p.Team == "ORDER"), "Enemy leaked into snapshot");
        Check(snapshot.CurrentHealth == 750 && snapshot.MaxHealth == 1300, "Health missing");
        Check(snapshot.ActivePlayer.Kills == 3 && snapshot.ActivePlayer.CreepScore == 90 && snapshot.ActivePlayer.Items[0].Id == 1001, "Own stats missing");
        Check(snapshot.Players[1].IsDead, "Ally death state missing");

        using var legacy = JsonDocument.Parse("""{"summonerName":"Player"}""");
        Check(LiveTeamParser.Parse(legacy.RootElement, roster.RootElement).ActivePlayer.Name == "Player#LAN", "Legacy identity failed");
        using var split = JsonDocument.Parse("""{"riotIdGameName":"Player","riotIdTagLine":"LAN"}""");
        Check(LiveTeamParser.Parse(split.RootElement, roster.RootElement).ActivePlayer.Name == "Player#LAN", "Split identity failed");
        using var unknown = JsonDocument.Parse("""{"riotId":"Unknown#LAN"}""");
        ExpectInvalid(() => LiveTeamParser.Parse(unknown.RootElement, roster.RootElement));
        using var duplicate = JsonDocument.Parse(Roster.Replace("Ally#LAN", "Player#LAN"));
        ExpectInvalid(() => LiveTeamParser.Parse(player.RootElement, duplicate.RootElement));
        using var missingTeam = JsonDocument.Parse(Roster.Replace("\"team\":\"ORDER\"", "\"team\":\"\""));
        ExpectInvalid(() => LiveTeamParser.Parse(player.RootElement, missingTeam.RootElement));
        using var missingOwnScores = JsonDocument.Parse(Roster.Replace("\"kills\":3", "\"kills\":null"));
        ExpectInvalid(() => LiveTeamParser.Parse(player.RootElement, missingOwnScores.RootElement));

        var handler = new LiveHandler();
        using var client = new LiveClient(handler);
        var sample = await client.ReadAsync(default);
        Check(sample.Gold == 1200 && sample.TeamSnapshot?.Players.Count == 2, "HTTP capture missing team");
        Check(handler.Paths.SequenceEqual(new[] { "activeplayer", "playerlist", "gamestats", "eventdata" }), "Unexpected endpoint");
        Check(!JsonSerializer.Serialize(sample).Contains("Enemy#LAN", StringComparison.Ordinal), "Enemy escaped adapter");
        Console.WriteLine("PASS: active player, allied roster, enemy exclusion, ambiguous identity rejection, health/items and HTTP capture.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void ExpectInvalid(Action action)
    {
        try { action(); } catch (JsonException) { return; }
        throw new Exception("Unidentified team accepted");
    }
    private sealed class LiveHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.Segments.Last();
            Paths.Add(path);
            var json = path switch
            {
                "activeplayer" => Active,
                "playerlist" => Roster,
                "gamestats" => """{"gameTime":600}""",
                "eventdata" => """{"Events":[]}""",
                _ => throw new InvalidOperationException(path)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }
}
