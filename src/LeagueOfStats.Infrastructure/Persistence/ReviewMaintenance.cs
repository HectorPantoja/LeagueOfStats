using Microsoft.Data.Sqlite;
namespace LeagueOfStats.Infrastructure.Persistence;

internal static class ReviewMaintenance
{
    public static Task RunAsync(SqliteConnection db, SqliteTransaction tx, DateTimeOffset now, CancellationToken token) =>
        SqliteSampleRepository.Execute(db, tx, """
            UPDATE MatchSessions SET Status='Interrupted',EndedUtc=LastSeenUtc,EndReason='StaleCapture'
            WHERE Status IN ('Capturing','Disconnected') AND julianday($now)-julianday(LastSeenUtc)>120.0/86400;
            DELETE FROM Samples WHERE julianday(RecordedUtc)<julianday($now)-30;
            DELETE FROM MatchSessions
            WHERE julianday(COALESCE(EndedUtc,LastSeenUtc,StartedUtc))<julianday($now)-90
                AND Status NOT IN ('Capturing','Disconnected')
                AND SessionId NOT IN (
                    SELECT SessionId FROM (
                        SELECT SessionId,ROW_NUMBER() OVER (
                            PARTITION BY PlayerKey,GameMode,MapNumber ORDER BY EndedUtc DESC,SessionId DESC) AS rank
                        FROM MatchSessions WHERE Status='Completed' AND FirstGameTime<=60 AND HasGaps=0
                            AND EventsIncomplete=0 AND DurationSeconds>=300 AND GameMode<>'UNKNOWN' AND MapNumber>0
                    ) WHERE rank<=10
                );
            """, token, ("$now", now.ToString("O")));
}
