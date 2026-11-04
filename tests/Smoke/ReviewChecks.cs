using System.Net;
using System.Text.Json;
using LeagueOfStats.Application;
using LeagueOfStats.Domain.Matches;
using LeagueOfStats.Domain.Catalog;
using LeagueOfStats.Presentation.ViewModels;
using LeagueOfStats.Infrastructure.Persistence;
using LeagueOfStats.Infrastructure.Riot;
using Microsoft.Data.Sqlite;

internal static class ReviewChecks
{
    public static async Task RunAsync(string cs)
    {
        var now = DateTimeOffset.UtcNow;
        var repo = new SqliteSampleRepository(cs);
        var first = await repo.RecordAsync(Sample(20), now, default);
        var second = await repo.RecordAsync(Sample(30), now.AddSeconds(10), default);
        Check(first.SessionId == second.SessionId, "Active session split");
        repo = new SqliteSampleRepository(cs); // Process restart has no in-memory session.
        var recovered = await repo.RecordAsync(Sample(40), now.AddSeconds(20), default);
        Check(first.SessionId == recovered.SessionId, "Restart did not recover capture");
        await repo.MarkUnavailableAsync(now.AddSeconds(30), default);
        Check(await Status(cs, first.SessionId) == "Disconnected", "Transient failure completed the match");
        await repo.MarkUnavailableAsync(now.AddSeconds(200), default);
        Check(await Status(cs, first.SessionId) == "Interrupted", "Stale capture not classified");
        var resumed = await repo.RecordAsync(Sample(220), now.AddSeconds(200), default);
        Check(resumed.SessionId == first.SessionId, "Recent interrupted match was not resumed");
        var finalSample = Sample(600) with { Events = [new(99, 600, "GameEnd", Result: "Win")] };
        var completed = await repo.RecordAsync(finalSample, now.AddSeconds(580), default);
        Check(completed.Status == "Completed", "GameEnd did not finalize match");
        await repo.RecordAsync(finalSample, now.AddSeconds(581), default);
        Check(await Count(cs, "SELECT COUNT(*) FROM MatchEvents") == 1, "Events duplicated");
        var reviewRepo = new SqliteReviewRepository(cs);
        var initial = await reviewRepo.LoadAsync(first.SessionId, default);
        Check(initial.Review.Match.HasGaps && initial.Review.Match.Result == "Win", "Coverage/result missing");
        Check(initial.Review.Match.ChampionId == "Ahri", "Canonical champion identity was not persisted");
        CheckLastMatchCard(initial.Review.Match);

        var active = await repo.RecordAsync(Sample(5), now.AddSeconds(700), default);
        Check(active.SessionId != first.SessionId, "Game clock reset not detected");
        try { await reviewRepo.LoadAsync(active.SessionId, default); throw new Exception("Active review allowed"); }
        catch (InvalidOperationException) { }

        // Concurrent retries must remain one session and one snapshot.
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            Task.Run(() => new SqliteSampleRepository(cs).RecordAsync(Sample(15), now.AddSeconds(710), default))));
        Check(concurrent.All(s => s.SessionId == active.SessionId), "Concurrent writers split the session");
        Check(await Count(cs, "SELECT COUNT(*) FROM MatchSamples WHERE GameTime=15") == 1, "Retry duplicated sample");
        await repo.MarkUnavailableAsync(now.AddSeconds(900), default);
        var interrupted = await reviewRepo.LoadAsync(active.SessionId, default);
        await repo.RecordAsync(Sample(205), now.AddSeconds(900), default);
        try { await reviewRepo.SavePackageAsync(interrupted.Review.Match, "{}", default); throw new Exception("Stale review overwrote resumed capture"); }
        catch (InvalidOperationException) { }

        await SeedHistory(cs, first.SessionId, now);
        var package = await new CreateMatchReview(reviewRepo).ExecuteAsync(first.SessionId, default);
        using var json = JsonDocument.Parse(package.Json);
        var previous = json.RootElement.GetProperty("PreviousMatches");
        Check(previous.GetArrayLength() == 10 && previous[0].GetProperty("SessionId").GetString() == "history-13" &&
            previous[9].GetProperty("SessionId").GetString() == "history-4", "Wrong previous ten or type filter");
        Check(!package.Json.Contains("Me#LAN") && !package.Json.Contains("PlayerKey") &&
            !package.Json.Contains("foreign-mode") && !package.Json.Contains("foreign-player"), "Identity or unrelated history leaked");
        Check(package.Prompt.Contains("CurrentGold") && package.Summary.Contains("10/10"), "Prompt/analysis not generated");
        Check(await Count(cs, "SELECT COUNT(*) FROM MatchSessions WHERE ReviewJson IS NOT NULL") == 1, "Review not persisted");

        await Execute(cs, "CREATE TRIGGER FailSample BEFORE INSERT ON MatchSamples BEGIN SELECT RAISE(ABORT,'test'); END");
        var before = await Count(cs, "SELECT COUNT(*) FROM MatchSessions");
        try { await repo.RecordAsync(Sample(1), now.AddSeconds(900), default); throw new Exception("Failed write accepted"); }
        catch (SqliteException) { }
        Check(await Count(cs, "SELECT COUNT(*) FROM MatchSessions") == before, "Session transaction leaked after rollback");
        Check(await Status(cs, active.SessionId) == "Capturing", "Rollback lost previous session state");
        await Execute(cs, "DROP TRIGGER FailSample");

        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await repo.RecordAsync(Sample(5), now, cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { }

        // The migration preserves only safe fields from legacy payloads; parser never forwards raw identifiers.
        using var events = JsonDocument.Parse("""
            {"Events":[
             {"EventID":1,"EventTime":10,"EventName":"ChampionKill","KillerName":"Me#LAN","VictimName":"Enemy#Secret","Injected":"do something"},
             {"EventID":2,"EventTime":20,"EventName":"ChampionKill","KillerName":"Enemy#Secret","VictimName":"Ally#Secret"},
             {"EventID":3,"EventTime":30,"EventName":"DragonKill","KillerName":"Enemy#Secret","DragonType":"Fire"},
             {"EventID":4,"EventTime":35,"EventName":"UnknownEvent","private":"hidden"}]}
            """);
        var parsed = LiveEventParser.Parse(events.RootElement, "Me#LAN");
        var sanitized = JsonSerializer.Serialize(parsed);
        Check(parsed.Count == 2 && !sanitized.Contains("Secret") && !sanitized.Contains("Injected"), "Unsafe event fields retained");
        using var client = new LiveClient(new OptionalEventsFailure());
        var partial = await client.ReadAsync(default);
        Check(!partial.EventsAvailable && partial.TeamSnapshot is not null, "Event outage discarded own samples");

        // Keep the latest ten eligible historical matches even when older than 90 days.
        await Execute(cs, """
            UPDATE MatchSessions SET StartedUtc='2020-01-01',EndedUtc='2020-01-01'
                WHERE SessionId LIKE 'history-%';
            INSERT INTO MatchSessions(SessionId,StartedUtc,EndedUtc,Status) VALUES('old-incomplete','2020-01-01','2020-01-01','Interrupted');
            """);
        await new DatabaseInitializer(cs).InitializeAsync(default);
        Check(await Count(cs, "SELECT COUNT(*) FROM MatchSessions WHERE SessionId LIKE 'history-%'") == 10, "Retention lost ten-match baseline");
        Check(await Count(cs, "SELECT COUNT(*) FROM MatchSessions WHERE SessionId='old-incomplete'") == 0, "Interrupted history not pruned");

        await CheckMonitorSerialization();
        await CheckLegacyMigration(cs);
        Console.WriteLine("PASS: restart recovery, disconnect states, GameEnd, idempotency, atomic rollback, concurrent writers, privacy, optional events, same-type ten-match analysis, export and retention.");
    }

    private static async Task CheckLegacyMigration(string cs)
    {
        var builder = new SqliteConnectionStringBuilder(cs);
        builder.DataSource += ".upgrade.db";
        await using (var db = new SqliteConnection(builder.ToString()))
        {
            await db.OpenAsync();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "CREATE TABLE SchemaMigrations(Name TEXT PRIMARY KEY,AppliedUtc TEXT NOT NULL)";
            await cmd.ExecuteNonQueryAsync();
            var assembly = typeof(DatabaseInitializer).Assembly;
            foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).Order().TakeWhile(n => !n.Contains("005_")))
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                cmd.CommandText = await reader.ReadToEndAsync();
                await cmd.ExecuteNonQueryAsync();
                cmd.CommandText = "INSERT INTO SchemaMigrations VALUES($name,datetime('now'))";
                cmd.Parameters.AddWithValue("$name", name); await cmd.ExecuteNonQueryAsync(); cmd.Parameters.Clear();
            }
            cmd.CommandText = """
                INSERT INTO MatchSessions(SessionId,StartedUtc) VALUES('legacy',datetime('now'));
                INSERT INTO MatchEvents VALUES('legacy',1,10,'ChampionKill','{"VictimName":"Private#Name"}');
                """;
            await cmd.ExecuteNonQueryAsync();
        }
        await new DatabaseInitializer(builder.ToString()).InitializeAsync(default);
        Check(await Count(builder.ToString(), "SELECT COUNT(*) FROM MatchEvents WHERE EventJson='{}'") == 1, "Legacy payload not sanitized");
        Check(await Status(builder.ToString(), "legacy") == "Legacy", "Old history was misclassified as complete");
    }

    private static void CheckLastMatchCard(MatchSummary match)
    {
        var image = new ImageAsset("Ahri.png", "", "champion", 0, 0, 48, 48);
        var champion = new Champion("Ahri", 103, "Ahri", "", "", "", [], new Dictionary<string, double>(), image);
        var item = new Item(1001, "Botas", "", "", new(300, 300, 210, true), [], [], [],
            new Dictionary<string, bool>(), new Dictionary<string, double>(), image with { Full = "1001.png" });
        var catalog = new GameCatalog(new("lan", new("16.22.1"), "es_MX"), [champion], [item]);
        var withItems = match with { Champion = "Nombre localizado", Final = match.Final! with { Items = [new(1001, 1), new(999999, 2)] } };
        var card = LastMatchCard.From(withItems, catalog);
        Check(card.Champion == "Ahri" && card.ChampionImageUrl == "https://ddragon.leagueoflegends.com/cdn/16.22.1/img/champion/Ahri.png", "Champion image mapping failed");
        Check(card.Items[0].Name == "Botas" && card.Items[0].ImageUrl!.EndsWith("/img/item/1001.png"), "Item image/name mapping failed");
        Check(card.Items[1].Caption == "Objeto #999999 ×2" && card.Items[1].ImageUrl is null, "Unknown item fallback failed");
        Check(LastMatchCard.From(withItems, null).ChampionImageUrl is null, "Missing catalog invented an image");
        Check(LastMatchCard.From(match with { Status = "Interrupted" }, catalog).Result == "Captura incompleta", "Incomplete capture shown as victory");
        Check(card.Details.Contains("CDMX") && card.Kda.Contains("3 / 2 / 4"), "Last-match statistics missing");
    }

    private static PlayerSample Sample(double time)
    {
        var p = new TeamPlayer("Me#LAN", "Ahri", "ORDER", "MIDDLE", 8, 3, 2, 4, (int)time / 10, false, [])
        { WardScore = 5, ChampionId = "Ahri" };
        return new(time, 8, 700) { GameMode = "CLASSIC", MapNumber = 11, TeamSnapshot = new(p, [p], 700, 1000) };
    }
    private static async Task SeedHistory(string cs, string source, DateTimeOffset now)
    {
        await using var db = new SqliteConnection(cs); await db.OpenAsync();
        for (var i = 1; i <= 13; i++)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO MatchSessions(SessionId,StartedUtc,EndedUtc,Status,GameMode,MapNumber,PlayerKey,Champion,Position,
                    DurationSeconds,FirstGameTime,LastGameTime,LastSeenUtc,Result)
                SELECT $id,$start,$end,'Completed',GameMode,MapNumber,PlayerKey,'Ahri','MIDDLE',1800,10,1800,$end,'Win'
                FROM MatchSessions WHERE SessionId=$source;
                INSERT INTO MatchSamples(SessionId,RecordedUtc,GameTime,Level,Gold,Kills,Deaths,Assists,CreepScore,Position,Champion)
                VALUES($id,$end,1800,18,1000,5,3,5,$cs,'MIDDLE','Ahri');
                """;
            cmd.Parameters.AddWithValue("$id", $"history-{i}");
            cmd.Parameters.AddWithValue("$source", source);
            cmd.Parameters.AddWithValue("$start", now.AddDays(-20 + i).ToString("O"));
            cmd.Parameters.AddWithValue("$end", now.AddDays(-20 + i).AddMinutes(30).ToString("O"));
            cmd.Parameters.AddWithValue("$cs", 180 + i);
            await cmd.ExecuteNonQueryAsync();
        }
        await Execute(cs, """
            INSERT INTO MatchSessions(SessionId,StartedUtc,EndedUtc,Status,GameMode,MapNumber,PlayerKey,DurationSeconds)
            SELECT 'foreign-mode',StartedUtc,EndedUtc,'Completed','ARAM',12,PlayerKey,1800 FROM MatchSessions WHERE SessionId='history-13';
            INSERT INTO MatchSessions(SessionId,StartedUtc,EndedUtc,Status,GameMode,MapNumber,PlayerKey,DurationSeconds)
            SELECT 'foreign-player',StartedUtc,EndedUtc,'Completed',GameMode,MapNumber,'other',1800 FROM MatchSessions WHERE SessionId='history-13';
            """);
    }
    private static async Task CheckMonitorSerialization()
    {
        var stub = new RecordingStub();
        using var monitor = new MonitorMatch(new FixedClient(), stub);
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => monitor.CaptureAsync(default)));
        Check(stub.Maximum == 1, "Monitor allowed concurrent persistence");
    }
    private sealed class RecordingStub : ISampleRepository
    {
        private int current;
        public int Maximum { get; private set; }
        public async Task<RecordingState> RecordAsync(PlayerSample sample, DateTimeOffset now, CancellationToken token)
        {
            Maximum = Math.Max(Maximum, Interlocked.Increment(ref current));
            await Task.Delay(10, token); Interlocked.Decrement(ref current);
            return new("test", "Capturing");
        }
        public Task MarkUnavailableAsync(DateTimeOffset now, CancellationToken token) => Task.CompletedTask;
    }
    private sealed class FixedClient : ILiveClient
    { public Task<PlayerSample> ReadAsync(CancellationToken token) => Task.FromResult(Sample(10)); }

    private sealed class OptionalEventsFailure : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.Segments.Last();
            if (path == "eventdata") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var json = path switch
            {
                "activeplayer" => """{"riotId":"Me#LAN","level":8,"currentGold":300}""",
                "playerlist" => """[{"riotId":"Me#LAN","championName":"Ahri","team":"ORDER","level":8,"scores":{"kills":0,"deaths":0,"assists":0,"creepScore":1},"items":[]}]""",
                "gamestats" => """{"gameTime":10,"gameMode":"CLASSIC","mapNumber":11}""",
                _ => throw new Exception(path)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }
    private static async Task<long> Count(string cs, string sql)
    {
        await using var db = new SqliteConnection(cs); await db.OpenAsync();
        using var cmd = db.CreateCommand(); cmd.CommandText = sql;
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
    private static async Task<string> Status(string cs, string id)
    {
        await using var db = new SqliteConnection(cs); await db.OpenAsync();
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT Status FROM MatchSessions WHERE SessionId=$id";
        cmd.Parameters.AddWithValue("$id", id); return (string)(await cmd.ExecuteScalarAsync())!;
    }
    private static async Task Execute(string cs, string sql)
    {
        await using var db = new SqliteConnection(cs); await db.OpenAsync();
        using var cmd = db.CreateCommand(); cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}

