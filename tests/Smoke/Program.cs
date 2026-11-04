using LeagueOfStats.Domain.Catalog;
using LeagueOfStats.Domain.Matches;
using LeagueOfStats.Infrastructure.Persistence;
using LeagueOfStats.Infrastructure.Riot;
using Microsoft.Data.Sqlite;
using System.Text.Json;

var path = Path.Combine(Path.GetTempPath(), "LeagueOfStats-smoke-" + Guid.NewGuid().ToString("N"));
await RegressionChecks.RunAsync();
await LiveTeamChecks.RunAsync();
await PresentationChecks.RunAsync();
try
{
    await DependencyInjectionChecks.RunAsync(Path.Combine(path, "di"));
    using var player = JsonDocument.Parse("""{"level":8,"currentGold":1234.5,"ignoredEnemyData":{}}""");
    using var game = JsonDocument.Parse("""{"gameTime":600}""");
    var sample = LiveClient.Parse(player.RootElement, game.RootElement);
    if (sample != new PlayerSample(600, 8, 1234.5)) throw new Exception("Parsing failed");
    using var invalid = JsonDocument.Parse("{}");
    try { LiveClient.Parse(invalid.RootElement, game.RootElement); throw new Exception("Invalid data accepted"); }
    catch (JsonException) { }
    var database = new SqliteDatabase(path);
    var store = new SqliteCatalogRepository(database.ConnectionString);
    var samples = new SqliteSampleRepository(database.ConnectionString);
    var initializer = new DatabaseInitializer(database.ConnectionString);
    await initializer.InitializeAsync(default);
    await initializer.InitializeAsync(default);
    await SynchronizationChecks.RunAsync(database.ConnectionString);
    await ReviewChecks.RunAsync(database.ConnectionString);
    await AssetChecks.RunAsync(Path.Combine(path, "images"));

    var image = new ImageAsset("a.png", "sprite.png", "champion", 0, 0, 48, 48);
    var champion = new Champion("Aatrox", 266, "Aatrox", "title", "blurb", "BloodWell", ["Fighter"], new Dictionary<string, double> { ["hp"] = 650 }, image);
    var item = new Item(1001, "Boots", "description", "", new ItemGold(300, 300, 210, true), [], [], ["Boots"], new Dictionary<string, bool> { ["11"] = true }, new Dictionary<string, double>(), image);
    GameCatalog Catalog(string version, string locale = "es_MX") => new(new RegionPatch("lan", new PatchVersion(version), locale), [champion], [item]);
    await store.SaveAsync(Catalog("1.0.0"), default);
    await store.SaveAsync(Catalog("1.0.0", "en_US"), default);
    await store.SaveAsync(Catalog("1.0.1"), default);
    var restored = await store.FindAsync("lan", "es_MX", default);
    if (restored?.Patch.Version.Value != "1.0.1" || restored.Champions[0].Stats["hp"] != 650 || restored.Items[0].Gold.Total != 300) throw new Exception("Catalog roundtrip failed");
    if ((await store.FindAsync("lan", "en_US", default))?.Patch.Version.Value != "1.0.0") throw new Exception("Locale isolation failed");
    try { _ = new PatchVersion("../bad"); throw new Exception("Invalid version accepted"); } catch (ArgumentException) { }
    try { _ = new GameCatalog(Catalog("1.0.0").Patch, [champion, champion], [item]); throw new Exception("Duplicate accepted"); } catch (ArgumentException) { }
    await using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(path, "leagueofstats.db") }.ToString());
    await db.OpenAsync();
    using var cmd = db.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM SchemaMigrations";
    if ((long)(await cmd.ExecuteScalarAsync())! != 6) throw new Exception("Migration history failed");
    cmd.CommandText = "CREATE TRIGGER FailItem BEFORE INSERT ON Items BEGIN SELECT RAISE(ABORT,'test rollback'); END";
    await cmd.ExecuteNonQueryAsync();
    try { await store.SaveAsync(Catalog("1.0.2"), default); throw new Exception("Expected write failure"); }
    catch (SqliteException) { }
    if ((await store.FindAsync("lan", "es_MX", default))?.Patch.Version.Value != "1.0.1") throw new Exception("Atomic rollback failed");
    var championJson = """{"version":"1.0.1","data":{"Aatrox":{"id":"Aatrox","key":"266","name":"Aatrox","image":{"full":"Aatrox.png","sprite":"champion0.png","group":"champion","x":0,"y":0,"w":48,"h":48},"stats":{"hp":650}}}}""";
    var itemJson = """{"version":"1.0.1","data":{"1001":{"name":"Boots","gold":{"base":300,"total":300,"sell":210,"purchasable":true},"image":{"full":"1001.png","sprite":"item0.png","group":"item","x":0,"y":0,"w":48,"h":48},"maps":{"11":true}}}}""";
    var championsDto = JsonSerializer.Deserialize<DataDragonEnvelope<ChampionDto>>(championJson, DataDragonClient.JsonOptions)!;
    var itemsDto = JsonSerializer.Deserialize<DataDragonEnvelope<ItemDto>>(itemJson, DataDragonClient.JsonOptions)!;
    var mapped = DataDragonClient.Map(Catalog("1.0.1").Patch, championsDto, itemsDto);
    if (mapped.Champions[0].Key != 266 || mapped.Items[0].Id != 1001 || !mapped.Items[0].Maps["11"]) throw new Exception("Riot mapping failed");
    var unnamedItems = JsonSerializer.Deserialize<DataDragonEnvelope<ItemDto>>(itemJson.Replace("\"name\":\"Boots\"", "\"name\":\"\""), DataDragonClient.JsonOptions)!;
    var unnamed = DataDragonClient.Map(Catalog("1.0.1").Patch, championsDto, unnamedItems);
    if (unnamed.Items[0].Name != "Objeto #1001" || unnamed.Items[0].Image.Full != "1001.png")
        throw new Exception("Official unnamed items prevented catalog/image loading");
    try { DataDragonClient.Map(Catalog("1.0.2").Patch, championsDto, itemsDto); throw new Exception("Mixed patch accepted"); }
    catch (JsonException) { }
    Console.WriteLine("PASS: API parsing, domain invariants, Riot DTO mapping, patch mismatch rejection, migrations, persistence, locale isolation and atomic rollback.");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(path)) Directory.Delete(path, true);
}




