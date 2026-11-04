using LeagueOfStats.Application;
using Microsoft.Data.Sqlite;
namespace LeagueOfStats.Infrastructure.Persistence;

public sealed class SqlitePatchNotesRepository(string connectionString) : IPatchNotesRepository
{
    public async Task SaveAsync(IReadOnlyList<Uri> links, CancellationToken token)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(token);
        using var tx = db.BeginTransaction();
        foreach (var link in links)
        {
            using var cmd = db.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = "INSERT OR IGNORE INTO PatchNotes(Url,DiscoveredUtc) VALUES($u,datetime('now'))";
            cmd.Parameters.AddWithValue("$u", link.AbsoluteUri);
            await cmd.ExecuteNonQueryAsync(token);
        }
        tx.Commit();
    }
}
