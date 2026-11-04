using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using LeagueOfStats.Domain.Matches;
using LeagueOfStats.Application;
namespace LeagueOfStats.Infrastructure.Riot;

public sealed class LiveClient : ILiveClient, IDisposable
{
    private readonly HttpClient http;
    private readonly IAppDiagnostics log;
    public LiveClient() : this(CreateHandler(), null) { }
    public LiveClient(IAppDiagnostics diagnostics) : this(CreateHandler(), diagnostics) { }

    public LiveClient(HttpMessageHandler handler, IAppDiagnostics? diagnostics = null)
    {
        log = diagnostics ?? NullDiagnostics.Instance;
        http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://127.0.0.1:2999/liveclientdata/"),
            Timeout = TimeSpan.FromSeconds(2)
        };
    }

    private static HttpMessageHandler CreateHandler()
    {
        // Self-signed TLS exception confined to this fixed loopback client.
        return new HttpClientHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (request, _, _, _) =>
                request.RequestUri is { Scheme: "https", Host: "127.0.0.1", Port: 2999 }
        };
    }
    public async Task<PlayerSample> ReadAsync(CancellationToken token)
    {
        try
        {
            var player = await http.GetFromJsonAsync<JsonElement>("activeplayer", token);
            var roster = await http.GetFromJsonAsync<JsonElement>("playerlist", token);
            var game = await http.GetFromJsonAsync<JsonElement>("gamestats", token);
            var snapshot = LiveTeamParser.Parse(player, roster);
            var sample = Parse(player, game) with { TeamSnapshot = snapshot };
            try
            {
                var events = await http.GetFromJsonAsync<JsonElement>("eventdata", token);
                return sample with { Events = LiveEventParser.Parse(events, snapshot.ActivePlayer.Name) };
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
            {
                log.Write("capture.events.unavailable", error: ex);
                return sample with { EventsAvailable = false };
            }
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.ConnectionError || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new GameUnavailableException(ex);
        }
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
        {
            throw new GameUnavailableException(ex);
        }
    }
    public static PlayerSample Parse(JsonElement player, JsonElement game)
    {
        if (player.ValueKind != JsonValueKind.Object || game.ValueKind != JsonValueKind.Object || !player.TryGetProperty("level", out var level) ||
            !player.TryGetProperty("currentGold", out var gold) ||
            !game.TryGetProperty("gameTime", out var time) ||
            level.ValueKind != JsonValueKind.Number || gold.ValueKind != JsonValueKind.Number || time.ValueKind != JsonValueKind.Number || !level.TryGetInt32(out var l) || !gold.TryGetDouble(out var g) ||
            !time.TryGetDouble(out var t) || l < 1 || !double.IsFinite(g) ||
            !double.IsFinite(t) || t < 0 || t >= TimeSpan.MaxValue.TotalSeconds || g < 0)
            throw new JsonException("Invalid active player data");
        var mode = game.TryGetProperty("gameMode", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        if (string.IsNullOrWhiteSpace(mode) || mode.Length > 40 || !mode.All(c => c is >= 'A' and <= 'Z' or '_')) mode = "UNKNOWN";
        var map = game.TryGetProperty("mapNumber", out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out var number) && number > 0 ? number : 0;
        return new PlayerSample(t, l, g) { GameMode = mode, MapNumber = map };
    }
    public void Dispose() => http.Dispose();
}




