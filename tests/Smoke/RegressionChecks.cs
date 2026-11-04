using System.Net;
using System.Text.Json;
using LeagueOfStats.Application;
using LeagueOfStats.Domain.Catalog;
using LeagueOfStats.Domain.Matches;
using LeagueOfStats.Infrastructure.Riot;

internal static class RegressionChecks
{
    public static async Task RunAsync()
    {
        using var game = JsonDocument.Parse("""{"gameTime":10}""");
        foreach (var json in new[] { "null", "[]", "42", """{"level":"8","currentGold":20}""", """{"level":8,"currentGold":null}""" })
        {
            using var player = JsonDocument.Parse(json);
            Throws<JsonException>(() => LiveClient.Parse(player.RootElement, game.RootElement));
        }
        Throws<ArgumentException>(() => new PatchVersion("1.0.0\n"));
        Throws<ArgumentException>(() => new RegionPatch("../lan", new("1.0.0"), "es_MX"));
        Throws<ArgumentOutOfRangeException>(() => new PlayerSample(double.NaN, 1, 0));

        var tags = new[] { "Fighter" };
        var stats = new Dictionary<string, double> { ["hp"] = 650 };
        var image = new ImageAsset("x", "x", "x", 0, 0, 48, 48);
        var champion = new Champion("Aatrox", 266, "Aatrox", "", "", "", tags, stats, image);
        var item = new Item(1, "Item", "", "", new(1, 1, 1, true), [], [], [], new Dictionary<string, bool>(), stats, image);
        var catalog = new GameCatalog(new("lan", new("1.0.0"), "es_MX"), [champion], [item]);
        tags[0] = "Changed";
        stats["hp"] = 1;
        Check(catalog.Champions[0].Tags[0] == "Fighter" && catalog.Champions[0].Stats["hp"] == 650 && catalog.Items[0].Stats["hp"] == 650, "Mutable aggregate");

        using var http = new HttpClient(new JsonHandler("""{"version":"1.0.0","data":null}"""));
        var riot = new DataDragonClient(http);
        try { await riot.DownloadAsync(catalog.Patch, default); throw new Exception("Null API data accepted"); }
        catch (JsonException) { }
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { await riot.GetVersionsAsync(cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { }
        Console.WriteLine("PASS: malformed API data, strict value objects, immutable catalog, session boundaries, HTTP cancellation.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }
}

