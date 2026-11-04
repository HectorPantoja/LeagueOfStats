using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using LeagueOfStats.Application;
using LeagueOfStats.Domain.Catalog;
namespace LeagueOfStats.Infrastructure.Riot;

public sealed class DataDragonClient(HttpClient http) : IRiotCatalogClient, IPatchVersionSource
{
    private const string Root = "https://ddragon.leagueoflegends.com/";
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { RespectNullableAnnotations = true };
    private async Task<T> GetAsync<T>(string path, CancellationToken token) =>
        await http.GetFromJsonAsync<T>(Root + path, JsonOptions, token) ?? throw new JsonException("Empty Riot response");
    public async Task<IReadOnlyList<PatchVersion>> GetVersionsAsync(CancellationToken token) =>
        (await GetAsync<string[]>("api/versions.json", token)).Select(v => new PatchVersion(v)).ToArray();
    public async Task<RegionPatch> GetRegionPatchAsync(string region, string locale, CancellationToken token)
    {
        _ = new RegionPatch(region, new PatchVersion("0.0.0"), locale);
        var realm = await GetAsync<RealmDto>($"realms/{region}.json", token);
        return new RegionPatch(region, new PatchVersion(realm.Version), locale);
    }
    public async Task<GameCatalog> DownloadAsync(RegionPatch patch, CancellationToken token)
    {
        var path = $"cdn/{patch.Version.Value}/data/{Uri.EscapeDataString(patch.Locale)}/";
        var champions = await GetAsync<DataDragonEnvelope<ChampionDto>>(path + "champion.json", token);
        var items = await GetAsync<DataDragonEnvelope<ItemDto>>(path + "item.json", token);
        return Map(patch, champions, items);
    }
    public async Task<ChampionDto> GetChampionDetailAsync(PatchVersion version, string locale, string id, CancellationToken token)
    {
        _ = new RegionPatch("lan", version, locale);
        if (id is null || !Regex.IsMatch(id, "\\A[A-Za-z0-9_]+\\z")) throw new ArgumentException("Invalid champion id", nameof(id));
        var result = await GetAsync<DataDragonEnvelope<ChampionDto>>($"cdn/{version.Value}/data/{Uri.EscapeDataString(locale)}/champion/{Uri.EscapeDataString(id)}.json", token);
        if (result.Version != version.Value || !result.Data.TryGetValue(id, out var champion)) throw new JsonException("Invalid champion detail");
        return champion;
    }
    public static GameCatalog Map(RegionPatch patch, DataDragonEnvelope<ChampionDto> champions, DataDragonEnvelope<ItemDto> items)
    {
        if (champions.Version != patch.Version.Value || items.Version != patch.Version.Value) throw new JsonException("Catalog version mismatch");
        var cs = champions.Data.Select(entry =>
        {
            var c = entry.Value;
            if (entry.Key != c.Id || !int.TryParse(c.Key, out var key)) throw new JsonException("Invalid champion identity");
            return new Champion(c.Id, key, c.Name, c.Title, c.Blurb, c.Partype, c.Tags, c.Stats, MapImage(c.Image));
        });
        var its = items.Data.Select(entry =>
        {
            if (!int.TryParse(entry.Key, out var id)) throw new JsonException("Invalid item identity");
            var i = entry.Value;
            // Some official entries have no localized name (including purchasable mode-specific items).
            // Preserve their identity and artwork instead of rejecting the whole regional catalog.
            var name = string.IsNullOrWhiteSpace(i.Name) ? $"Objeto #{id}" : i.Name;
            return new Item(id, name, i.Description, i.Plaintext, new ItemGold(i.Gold.Base, i.Gold.Total, i.Gold.Sell, i.Gold.Purchasable),
                i.From, i.Into, i.Tags, i.Maps, i.Stats, MapImage(i.Image));
        });
        return new GameCatalog(patch, cs, its);
    }
    private static ImageAsset MapImage(ImageDto i) => new(i.Full, i.Sprite, i.Group, i.X, i.Y, i.W, i.H);
}


