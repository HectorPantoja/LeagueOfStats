using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeagueOfStats.Application;
using LeagueOfStats.Domain.Matches;
using LeagueOfStats.Infrastructure.Riot;
using Microsoft.Data.Sqlite;
namespace LeagueOfStats.Infrastructure.Persistence;

public sealed class SqliteSampleRepository(string connectionString, IAppDiagnostics? diagnostics = null) : ISampleRepository
{
    private readonly IAppDiagnostics log = diagnostics ?? NullDiagnostics.Instance;

    public Task<RecordingState> RecordAsync(PlayerSample sample, DateTimeOffset now, CancellationToken token) =>
        SqliteRetry.ExecuteAsync(() => RecordCoreAsync(sample, now, token), token);

    private async Task<RecordingState> RecordCoreAsync(PlayerSample sample, DateTimeOffset now, CancellationToken token)
    {
        var player = sample.TeamSnapshot?.ActivePlayer ?? throw new InvalidOperationException("Active player is required for recording.");
        await using var db = new SqliteConnection(connectionString);
        await db.OpenAsync(token);
        using var tx = db.BeginTransaction();
        var salt = (string)(await Scalar(db, tx, "SELECT Value FROM LocalSettings WHERE Name='PlayerKeySalt'", token))!;
        var key = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(salt), Encoding.UTF8.GetBytes(player.Name)));
        string? id = null;
        string? previousStatus = null;
        double previousTime = -1;
        double gap = 0;
        using (var cmd = Command(db, tx, """
            SELECT SessionId,LastGameTime,LastSeenUtc,Status,Champion
            FROM MatchSessions WHERE PlayerKey=$key AND GameMode=$mode AND MapNumber=$map
            ORDER BY LastSeenUtc DESC LIMIT 1
            """, ("$key", key), ("$mode", sample.GameMode), ("$map", sample.MapNumber)))
        using (var reader = await cmd.ExecuteReaderAsync(token))
        {
            if (await reader.ReadAsync(token) && !reader.IsDBNull(2))
            {
                previousTime = reader.GetDouble(1);
                var lastSeen = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture);
                gap = (now - lastSeen).TotalSeconds;
                previousStatus = reader.GetString(3);
                var gameDelta = sample.GameTime - previousTime;
                var continuation = gap >= 0 && gap < 600 && gameDelta >= -0.5 &&
                    Math.Abs(gameDelta - gap) < 90 && reader.GetString(4) == player.Champion;
                if (previousStatus == "Completed")
                    continuation = continuation && Math.Abs(gameDelta) < 1 && sample.Events.Any(e => e.EventName == "GameEnd");
                if (continuation) id = reader.GetString(0);
            }
        }
        if (id is not null && previousStatus == "Completed")
        {
            tx.Commit();
            return new(id, "Completed");
        }
        var created = id is null;
        id ??= Guid.NewGuid().ToString("N");
        if (created)
        {
            await Execute(db, tx, """
                UPDATE MatchSessions SET Status='Interrupted',EndedUtc=LastSeenUtc,EndReason='NewCapture'
                WHERE Status IN ('Capturing','Disconnected');
                INSERT INTO MatchSessions(SessionId,StartedUtc,GameMode,MapNumber,PlayerKey,Champion,Position,
                    Status,FirstGameTime,LastGameTime,LastSeenUtc)
                VALUES($id,$utc,$mode,$map,$key,$champion,$position,'Capturing',$time,$time,$utc)
                """, token, ("$id", id), ("$utc", now.ToString("O")), ("$mode", sample.GameMode),
                ("$map", sample.MapNumber), ("$key", key), ("$champion", player.Champion),
                ("$position", player.Position), ("$time", sample.GameTime));
        }
        var events = sample.Events.Where(e => LiveEventParser.AllowedNames.Contains(e.EventName) &&
            e.EventId >= 0 && double.IsFinite(e.EventTime) && e.EventTime >= 0 && e.EventTime <= sample.GameTime + 10).ToArray();
        var end = events.LastOrDefault(e => e.EventName == "GameEnd");
        var result = end?.Result is "Win" or "Lose" ? end.Result : "Unknown";
        foreach (var e in events)
        {
            // Normalize again at the persistence boundary; never store arbitrary source JSON.
            var sanitized = e with
            {
                Participation = e.Participation is "Kill" or "Death" or "Assist" ? e.Participation : null,
                Objective = e.Objective is "Air" or "Earth" or "Fire" or "Water" or "Hextech" or "Chemtech" or "Elder" ? e.Objective : null,
                Result = e.Result is "Win" or "Lose" ? e.Result : null
            };
            await Execute(db, tx, """
                INSERT OR IGNORE INTO MatchEvents(SessionId,EventId,EventTime,EventName,EventJson)
                VALUES($id,$event,$time,$name,$json)
                """, token, ("$id", id), ("$event", e.EventId), ("$time", e.EventTime),
                ("$name", e.EventName), ("$json", JsonSerializer.Serialize(sanitized)));
        }
        // Persist at most one unchanged snapshot every 10 game seconds, plus changes in K/D/A and the end.
        bool save = created || end is not null;
        using (var cmd = Command(db, tx, """
            SELECT GameTime,Kills,Deaths,Assists FROM MatchSamples WHERE SessionId=$id ORDER BY GameTime DESC,Id DESC LIMIT 1
            """, ("$id", id)))
        using (var reader = await cmd.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token)) save = true;
            else save |= sample.GameTime - reader.GetDouble(0) >= 10 ||
                player.Kills != reader.GetInt32(1) || player.Deaths != reader.GetInt32(2) || player.Assists != reader.GetInt32(3);
        }
        if (save)
            await Execute(db, tx, """
                INSERT INTO MatchSamples(SessionId,RecordedUtc,GameTime,Level,Gold,Kills,Deaths,Assists,CreepScore,
                    Position,Champion,WardScore,ItemsJson,IsDead,CurrentHealth,MaxHealth)
                SELECT $id,$utc,$time,$level,$gold,$kills,$deaths,$assists,$cs,$position,$champion,$ward,$items,$dead,$health,$max
                WHERE NOT EXISTS(SELECT 1 FROM MatchSamples WHERE SessionId=$id AND GameTime=$time)
                """, token, ("$id", id), ("$utc", now.ToString("O")), ("$time", sample.GameTime), ("$level", sample.Level),
                ("$gold", sample.Gold), ("$kills", player.Kills), ("$deaths", player.Deaths), ("$assists", player.Assists),
                ("$cs", player.CreepScore), ("$position", player.Position), ("$champion", player.Champion),
                ("$ward", player.WardScore), ("$items", JsonSerializer.Serialize(player.Items.Select(i => new { i.Id, i.Count }))),
                ("$dead", player.IsDead ? 1 : 0), ("$health", sample.TeamSnapshot.CurrentHealth), ("$max", sample.TeamSnapshot.MaxHealth));
        var status = end is null ? "Capturing" : "Completed";
        await Execute(db, tx, """
            UPDATE MatchSessions SET LastSeenUtc=$utc,LastGameTime=$time,DurationSeconds=$time,Status=$status,
                EndedUtc=$ended,EndReason=$reason,Result=$result,Position=$position,ChampionId=$championId,
                HasGaps=MAX(HasGaps,$gap),EventsIncomplete=$missing
            WHERE SessionId=$id
            """, token, ("$utc", now.ToString("O")), ("$time", sample.GameTime), ("$status", status),
            ("$ended", end is null ? null : now.ToString("O")), ("$reason", end is null ? null : "GameEnd"),
            ("$result", result), ("$position", player.Position), ("$gap", !created && gap > 30 ? 1 : 0),
            ("$missing", sample.EventsAvailable ? 0 : 1), ("$id", id), ("$championId", player.ChampionId));
        if (end is not null) await ReviewMaintenance.RunAsync(db, tx, now, token);
        tx.Commit();
        if (created) log.Write("session.created", id);
        else if (previousStatus is "Interrupted" or "Disconnected") log.Write("session.recovered", id);
        if (end is not null) log.Write("session.completed", id);
        return new(id, status);
    }

    public Task MarkUnavailableAsync(DateTimeOffset now, CancellationToken token) =>
        SqliteRetry.ExecuteAsync(async () =>
        {
            await using var db = new SqliteConnection(connectionString);
            await db.OpenAsync(token);
            using var tx = db.BeginTransaction();
            await Execute(db, tx, """
                UPDATE MatchSessions SET
                    Status=CASE WHEN julianday($now)-julianday(LastSeenUtc) >= 120.0/86400 THEN 'Interrupted' ELSE 'Disconnected' END,
                    EndedUtc=CASE WHEN julianday($now)-julianday(LastSeenUtc) >= 120.0/86400 THEN LastSeenUtc ELSE NULL END,
                    EndReason='ConnectionLost'
                WHERE Status IN ('Capturing','Disconnected')
                """, token, ("$now", now.ToString("O")));
            tx.Commit();
            return true;
        }, token);

    internal static SqliteCommand Command(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    {
        var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }
    internal static async Task Execute(SqliteConnection db, SqliteTransaction tx, string sql, CancellationToken token, params (string, object?)[] args)
    {
        using var cmd = Command(db, tx, sql, args); await cmd.ExecuteNonQueryAsync(token);
    }
    private static async Task<object?> Scalar(SqliteConnection db, SqliteTransaction tx, string sql, CancellationToken token)
    {
        using var cmd = Command(db, tx, sql); return await cmd.ExecuteScalarAsync(token);
    }
}

