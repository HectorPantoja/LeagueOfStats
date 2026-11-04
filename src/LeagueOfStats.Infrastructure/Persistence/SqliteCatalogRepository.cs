using System.Text.Json;
using LeagueOfStats.Application;
using LeagueOfStats.Domain.Catalog;
using Microsoft.Data.Sqlite;
namespace LeagueOfStats.Infrastructure.Persistence;

public sealed class SqliteCatalogRepository(string connectionString) : ICatalogRepository
{
    public async Task<GameCatalog?> FindAsync(string region, string locale, CancellationToken token)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(token);
        using var tx = db.BeginTransaction(deferred: true);
        using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.Parameters.AddWithValue("$r", region); cmd.Parameters.AddWithValue("$l", locale);
        cmd.CommandText = "SELECT Version FROM GameCatalogs WHERE Region=$r AND Locale=$l";
        if (await cmd.ExecuteScalarAsync(token) is not string version) return null;
        async Task<List<T>> Read<T>(string table)
        {
            cmd.CommandText = $"SELECT Payload FROM {table} WHERE Region=$r AND Locale=$l";
            using var reader = await cmd.ExecuteReaderAsync(token);
            var rows = new List<T>();
            while (await reader.ReadAsync(token)) rows.Add(JsonSerializer.Deserialize<T>(reader.GetString(0)) ?? throw new JsonException("Null catalog payload"));
            return rows;
        }
        var champions = await Read<Champion>("Champions"); var items = await Read<Item>("Items");
        tx.Commit();
        return new GameCatalog(new RegionPatch(region, new PatchVersion(version), locale), champions, items);
    }
    public async Task SaveAsync(GameCatalog catalog, CancellationToken token)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(token);
        using var tx = db.BeginTransaction(deferred: true);
        using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.Parameters.AddWithValue("$r", catalog.Patch.Region); cmd.Parameters.AddWithValue("$l", catalog.Patch.Locale);
        cmd.Parameters.AddWithValue("$v", catalog.Patch.Version.Value);
        cmd.CommandText = "DELETE FROM GameCatalogs WHERE Region=$r AND Locale=$l; INSERT INTO GameCatalogs(Region,Locale,Version,UpdatedUtc) VALUES($r,$l,$v,datetime('now'));";
        await cmd.ExecuteNonQueryAsync(token);
        foreach (var champion in catalog.Champions)
            await Insert(champion.Id, champion.Name, champion, champion.Key);
        foreach (var item in catalog.Items) await Insert(item.Id, item.Name, item, null);
        tx.Commit();
        async Task Insert<T>(object id, string name, T entity, int? key)
        {
            using var insert = db.CreateCommand(); insert.Transaction = tx;
            insert.CommandText = key.HasValue
                ? "INSERT INTO Champions(Region,Locale,Id,NumericKey,Name,Payload) VALUES($r,$l,$id,$key,$name,$json)"
                : "INSERT INTO Items(Region,Locale,Id,Name,Payload) VALUES($r,$l,$id,$name,$json)";
            insert.Parameters.AddWithValue("$r", catalog.Patch.Region); insert.Parameters.AddWithValue("$l", catalog.Patch.Locale);
            insert.Parameters.AddWithValue("$id", id); insert.Parameters.AddWithValue("$name", name);
            insert.Parameters.AddWithValue("$json", JsonSerializer.Serialize(entity));
            if (key.HasValue) insert.Parameters.AddWithValue("$key", key.Value);
            await insert.ExecuteNonQueryAsync(token);
        }
    }
}


