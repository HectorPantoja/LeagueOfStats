using Microsoft.Data.Sqlite;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Uso: CreateDatabase <ruta-del-archivo.db>");
    return 1;
}

try
{
    var path = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    // Reserve the name exclusively: an existing database is never overwritten.
    bool created;
    try
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        created = true;
    }
    catch (IOException) when (File.Exists(path)) { created = false; }

    using var db = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = created ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly,
        Pooling = false
    }.ToString());
    db.Open();
    using var command = db.CreateCommand();
    // VACUUM writes a valid SQLite header even before application tables exist.
    command.CommandText = created ? "VACUUM;" : "SELECT COUNT(*) FROM sqlite_schema;";
    command.ExecuteScalar();
    Console.WriteLine(created ? $"Base SQLite creada: {path}" : $"Base SQLite existente conservada: {path}");
    Console.WriteLine("Las tablas y migraciones se aplican al iniciar LeagueOfStats.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error al preparar SQLite: {ex.Message}");
    return 1;
}
