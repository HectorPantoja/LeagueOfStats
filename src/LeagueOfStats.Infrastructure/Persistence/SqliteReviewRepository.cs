using System.Text.Json;
using LeagueOfStats.Application;
using LeagueOfStats.Domain.Matches;
using LeagueOfStats.Infrastructure.Riot;
using Microsoft.Data.Sqlite;
namespace LeagueOfStats.Infrastructure.Persistence;

public sealed class SqliteReviewRepository(string connectionString) : IReviewRepository
{
    private const string Selection = """
        SELECT s.SessionId,s.StartedUtc,s.EndedUtc,s.Status,s.GameMode,s.MapNumber,s.Champion,s.Position,
            s.DurationSeconds,s.FirstGameTime,s.HasGaps,s.EventsIncomplete,s.Result,
            p.Id,p.GameTime,p.Level,p.Gold,p.Kills,p.Deaths,p.Assists,p.CreepScore,p.WardScore,p.IsDead,p.CurrentHealth,p.MaxHealth,p.ItemsJson,s.ChampionId
        FROM MatchSessions s LEFT JOIN MatchSamples p ON p.Id=(
            SELECT Id FROM MatchSamples WHERE SessionId=s.SessionId ORDER BY GameTime DESC,Id DESC LIMIT 1)
        """;

    public async Task<IReadOnlyList<MatchSummary>> ListAsync(CancellationToken token)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(token);
        return await ReadSummaries(db, null, Selection + " ORDER BY s.StartedUtc DESC,s.SessionId DESC LIMIT 100", token);
    }

    public async Task<(MatchReview Review, IReadOnlyList<MatchSummary> Previous)> LoadAsync(string sessionId, CancellationToken token)
    {
        await using var db = new SqliteConnection(connectionString); await db.OpenAsync(token);
        using var tx = db.BeginTransaction(deferred: true);
        var matches = await ReadSummaries(db, tx, Selection + " WHERE s.SessionId=$id", token, ("$id", sessionId));
        var match = matches.SingleOrDefault() ?? throw new InvalidOperationException("La partida ya no está disponible.");
        if (!match.CanReview) throw new InvalidOperationException("La partida sigue activa.");
        var points = new List<ReviewPoint>();
        using (var cmd = SqliteSampleRepository.Command(db, tx, """
            SELECT GameTime,Level,Gold,Kills,Deaths,Assists,CreepScore,WardScore,IsDead,CurrentHealth,MaxHealth,ItemsJson
            FROM MatchSamples WHERE SessionId=$id ORDER BY GameTime,Id
            """, ("$id", sessionId)))
        using (var reader = await cmd.ExecuteReaderAsync(token))
            while (await reader.ReadAsync(token)) points.Add(Point(reader, 0));
        var events = new List<MatchEvent>();
        using (var cmd = SqliteSampleRepository.Command(db, tx, """
            SELECT EventJson FROM MatchEvents WHERE SessionId=$id ORDER BY EventTime,EventId
            """, ("$id", sessionId)))
        using (var reader = await cmd.ExecuteReaderAsync(token))
            while (await reader.ReadAsync(token))
            {
                // Legacy raw payloads were cleared by migration. Never include unvalidated payloads in an export.
                var e = JsonSerializer.Deserialize<MatchEvent>(reader.GetString(0));
                if (e is not null && e.EventName is not null && LiveEventParser.AllowedNames.Contains(e.EventName))
                    events.Add(e);
            }
        var previous = await ReadSummaries(db, tx, Selection + """
             WHERE s.PlayerKey=(SELECT PlayerKey FROM MatchSessions WHERE SessionId=$id)
                AND s.PlayerKey<>'' AND s.GameMode=$mode AND s.MapNumber=$map
                AND s.Status='Completed' AND s.FirstGameTime<=60 AND s.HasGaps=0 AND s.EventsIncomplete=0
                AND s.DurationSeconds>=300 AND s.SessionId<>$id AND s.EndedUtc<=$start
                AND s.GameMode<>'UNKNOWN' AND s.MapNumber>0
             ORDER BY s.EndedUtc DESC,s.SessionId DESC LIMIT 10
            """, token, ("$id", sessionId), ("$mode", match.GameMode), ("$map", match.MapNumber), ("$start", match.StartedUtc));
        tx.Commit();
        return (new(match, points.AsReadOnly(), events.AsReadOnly()), previous);
    }

    public Task SavePackageAsync(MatchSummary expected, string json, CancellationToken token) =>
        SqliteRetry.ExecuteAsync(async () =>
        {
            await using var db = new SqliteConnection(connectionString); await db.OpenAsync(token);
            using var cmd = SqliteSampleRepository.Command(db, null, """
                UPDATE MatchSessions SET ReviewJson=$json
                WHERE SessionId=$id AND Status IN ('Completed','Interrupted','Legacy')
                    AND Status=$status AND DurationSeconds=$duration
                """, ("$id", expected.SessionId), ("$json", json), ("$status", expected.Status), ("$duration", expected.DurationSeconds));
            if (await cmd.ExecuteNonQueryAsync(token) != 1)
                throw new InvalidOperationException("La captura se reanudó o ya no está disponible; vuelve a generar el resumen.");
            return true;
        }, token);

    private static async Task<IReadOnlyList<MatchSummary>> ReadSummaries(SqliteConnection db, SqliteTransaction? tx,
        string sql, CancellationToken token, params (string, object?)[] args)
    {
        using var cmd = SqliteSampleRepository.Command(db, tx, sql, args);
        using var r = await cmd.ExecuteReaderAsync(token);
        var result = new List<MatchSummary>();
        while (await r.ReadAsync(token))
            result.Add(new(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3),
                r.GetString(4), r.GetInt32(5), r.GetString(6), r.GetString(7), r.GetDouble(8), r.GetDouble(9),
                r.GetInt32(10) != 0, r.GetInt32(11) != 0, r.GetString(12), r.IsDBNull(13) ? null : Point(r, 14), r.GetString(26)));
        return result.AsReadOnly();
    }
    private static ReviewPoint Point(SqliteDataReader r, int offset) =>
        new(r.GetDouble(offset), r.GetInt32(offset + 1), r.GetDouble(offset + 2), r.GetInt32(offset + 3),
            r.GetInt32(offset + 4), r.GetInt32(offset + 5), r.GetInt32(offset + 6),
            Number(r, offset + 7), r.GetInt32(offset + 8) != 0, Number(r, offset + 9), Number(r, offset + 10),
            JsonSerializer.Deserialize<ReviewItem[]>(r.GetString(offset + 11)) ?? []);
    private static double? Number(SqliteDataReader r, int column) => r.IsDBNull(column) ? null : r.GetDouble(column);
}

