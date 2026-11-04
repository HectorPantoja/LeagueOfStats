using System.Globalization;
using LeagueOfStats.Application.Synchronization;
using LeagueOfStats.Domain.Synchronization;
using Microsoft.Data.Sqlite;
namespace LeagueOfStats.Infrastructure.Persistence;

public sealed class SqliteSyncStateStore(string connectionString) : ISyncStateStore
{
    public async Task<DateTimeOffset?> GetAnchorAsync(CancellationToken token)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(token);
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT FirstSuccessUtc FROM SyncAnchor WHERE Id=1";
        return Parse(await cmd.ExecuteScalarAsync(token) as string);
    }
    public async Task<SyncState> GetAsync(string flow, CancellationToken token)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(token);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT LastAttemptUtc,LastSuccessUtc,Summary FROM SyncState WHERE Flow=$f";
        cmd.Parameters.AddWithValue("$f", flow);
        using var reader = await cmd.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return new(null, null, null);
        return new(Parse(reader.IsDBNull(0) ? null : reader.GetString(0)), Parse(reader.IsDBNull(1) ? null : reader.GetString(1)), reader.IsDBNull(2) ? null : reader.GetString(2));
    }
    public async Task AttemptAsync(string flow, DateTimeOffset now, CancellationToken token)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(token);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO SyncState(Flow,LastAttemptUtc) VALUES($f,$t) ON CONFLICT(Flow) DO UPDATE SET LastAttemptUtc=$t";
        cmd.Parameters.AddWithValue("$f", flow); cmd.Parameters.AddWithValue("$t", now.ToString("O"));
        await cmd.ExecuteNonQueryAsync(token);
    }
    public async Task CompleteAsync(string flow, DateTimeOffset now, string summary, CancellationToken token)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(token);
        using var tx = db.BeginTransaction(); using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR IGNORE INTO SyncAnchor VALUES(1,$t);
            INSERT INTO SyncState(Flow,LastSuccessUtc,Summary) VALUES($f,$t,$s)
            ON CONFLICT(Flow) DO UPDATE SET LastSuccessUtc=$t,Summary=$s;
            """;
        cmd.Parameters.AddWithValue("$f", flow); cmd.Parameters.AddWithValue("$t", now.ToString("O")); cmd.Parameters.AddWithValue("$s", summary);
        await cmd.ExecuteNonQueryAsync(token); tx.Commit();
    }
    private static DateTimeOffset? Parse(string? value) => value is null ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
