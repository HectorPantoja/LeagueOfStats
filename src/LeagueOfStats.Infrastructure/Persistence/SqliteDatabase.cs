using Microsoft.Data.Sqlite;
namespace LeagueOfStats.Infrastructure.Persistence;

public sealed class SqliteDatabase
{
    public string ConnectionString { get; }

    public SqliteDatabase(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeagueOfStats");
        Directory.CreateDirectory(directory);
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "leagueofstats.db"),
            ForeignKeys = true,
            DefaultTimeout = 3
        }.ToString();
    }
}
