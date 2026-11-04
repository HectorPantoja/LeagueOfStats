using System.Net;
using LeagueOfStats.Application;
using LeagueOfStats.Infrastructure.Riot;

internal static class AssetChecks
{
    private const string Url = "https://ddragon.leagueoflegends.com/cdn/16.22.1/img/champion/Caitlyn.png";
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");

    public static async Task RunAsync(string directory)
    {
        var handler = new ImageHandler(Png);
        using (var cache = new DataDragonImageCache(new HttpClient(handler), directory, NullDiagnostics.Instance))
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => cache.GetAsync(Url, default)));
            Check(results.All(r => r is not null && r == results[0]) && handler.Calls == 1, "Asset requests were not coalesced");
            var path = new Uri(results[0]!).LocalPath;
            Check(File.ReadAllBytes(path).SequenceEqual(Png), "Cached image bytes changed");
            File.WriteAllText(path, "broken");
            Check(await cache.GetAsync(Url, default) is not null && handler.Calls == 2, "Corrupt cache was not repaired");
            Check(await cache.GetAsync("https://example.com/icon.png", default) is null, "Unofficial image host allowed");
            Check(await cache.GetAsync("https://ddragon.leagueoflegends.com/cdn/16.22.1/img/champion/%2e%2e/secret.png", default) is null, "Unsafe path allowed");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await cache.GetAsync(Url, cancelled.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { }
        }
        var offline = new ImageHandler([]) { Offline = true };
        using (var restarted = new DataDragonImageCache(new HttpClient(offline), directory, NullDiagnostics.Instance))
            Check(await restarted.GetAsync(Url, default) is not null && offline.Calls == 0, "Offline cache was not reused after restart");
        var invalid = new ImageHandler("<html>Error</html>"u8.ToArray());
        using (var cache = new DataDragonImageCache(new HttpClient(invalid), Path.Combine(directory, "invalid"), NullDiagnostics.Instance))
        {
            Check(await cache.GetAsync(Url, default) is null, "Non-PNG accepted");
            Check(await cache.GetAsync(Url, default) is null && invalid.Calls == 1, "Failed asset retried without backoff");
        }
        Console.WriteLine("PASS: official asset cache, concurrent loading, disk repair, offline restart, host/path restrictions, image validation and cancellation.");
    }

    private sealed class ImageHandler(byte[] bytes) : HttpMessageHandler
    {
        public int Calls;
        public bool Offline { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            await Task.Delay(10, token);
            if (Offline) throw new HttpRequestException("offline");
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
