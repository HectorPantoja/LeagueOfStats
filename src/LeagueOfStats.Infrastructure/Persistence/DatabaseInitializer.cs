using LeagueOfStats.Application;
using Microsoft.Data.Sqlite;
namespace LeagueOfStats.Infrastructure.Persistence;

public sealed class DatabaseInitializer(string connectionString) : IApplicationInitializer
{
    public async Task InitializeAsync(CancellationToken token)
    {
        await using var db = new SqliteConnection(connectionString);
        await db.OpenAsync(token);
        using (var setup = db.CreateCommand())
        {
            setup.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS SchemaMigrations(Name TEXT PRIMARY KEY, AppliedUtc TEXT NOT NULL);";
            await setup.ExecuteNonQueryAsync(token);
        }
        var assembly = typeof(DatabaseInitializer).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).Order(StringComparer.Ordinal))
        {
            using var transaction = db.BeginTransaction();
            using var cmd = db.CreateCommand(); cmd.Transaction = transaction;
            cmd.CommandText = "SELECT COUNT(*) FROM SchemaMigrations WHERE Name=$n";
            cmd.Parameters.AddWithValue("$n", name);
            if ((long)(await cmd.ExecuteScalarAsync(token))! == 0)
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                cmd.CommandText = await reader.ReadToEndAsync(token);
                await cmd.ExecuteNonQueryAsync(token);
                cmd.CommandText = "INSERT INTO SchemaMigrations VALUES($n,datetime('now'))";
                await cmd.ExecuteNonQueryAsync(token);
            }
            transaction.Commit();
        }
        using var maintenance = db.BeginTransaction();
        await ReviewMaintenance.RunAsync(db, maintenance, DateTimeOffset.UtcNow, token);
        maintenance.Commit();
    }
}

