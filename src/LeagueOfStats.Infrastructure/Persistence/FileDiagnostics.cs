using System.Text.Json;
using LeagueOfStats.Application;
namespace LeagueOfStats.Infrastructure.Persistence;

public sealed class FileDiagnostics : IAppDiagnostics
{
    private readonly string directory;
    private readonly object gate = new();
    private readonly Dictionary<string, (long Count, DateTimeOffset Last)> counters = new();
    public FileDiagnostics(string directory)
    {
        this.directory = directory;
        Directory.CreateDirectory(directory);
    }
    public void Write(string code, string? sessionId = null, Exception? error = null)
    {
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            counters.TryGetValue(code, out var state);
            state.Count++;
            if (now - state.Last < TimeSpan.FromSeconds(30)) { counters[code] = state; return; }
            counters[code] = (state.Count, now);
            try
            {
                var path = Path.Combine(directory, $"operations-{now:yyyy-MM-dd}.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length >= 5 * 1024 * 1024) return;
                // Exception messages and stack payloads can contain names, paths or credentials.
                File.AppendAllText(path, JsonSerializer.Serialize(new
                {
                    Utc = now, Code = code, Count = state.Count, Session = sessionId,
                    ErrorType = error?.GetType().Name,
                    SqliteCode = (error as Microsoft.Data.Sqlite.SqliteException)?.SqliteErrorCode
                }) + Environment.NewLine);
                foreach (var old in Directory.EnumerateFiles(directory, "operations-*.jsonl"))
                    if (File.GetLastWriteTimeUtc(old) < now.UtcDateTime.AddDays(-14)) File.Delete(old);
            }
            catch (IOException) { /* Diagnostics must not stop recording. */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
