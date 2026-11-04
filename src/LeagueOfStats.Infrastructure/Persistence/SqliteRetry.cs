using Microsoft.Data.Sqlite;
namespace LeagueOfStats.Infrastructure.Persistence;

internal static class SqliteRetry
{
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { return await operation(); }
            catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6 && attempt < 2)
            { await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), token); }
        }
    }
}

