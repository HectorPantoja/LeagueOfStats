using System.Net;
using LeagueOfStats.Application;
using LeagueOfStats.Application.Synchronization;
using LeagueOfStats.Domain.Synchronization;
using LeagueOfStats.Infrastructure.Persistence;
using LeagueOfStats.Infrastructure.Riot;

internal static class SynchronizationChecks
{
    public static async Task RunAsync(string connectionString)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
        var schedule = new FortnightlySchedule(zone);
        DateTimeOffset At(int day, int hour = 12) => new(2026, 11, day, hour, 0, 0, TimeSpan.FromHours(-6));
        var anchor = At(3); // Tuesday, week 0.
        var state = new SyncState(anchor, anchor, "ok");
        Check(!schedule.IsDue(At(3, 14), anchor, state), "Same-day repeated sync");
        Check(schedule.IsDue(At(4), anchor, state), "Wednesday not due");
        state = new(At(4), At(4), "ok");
        Check(schedule.IsDue(At(5), anchor, state), "Thursday not due");
        state = new(At(5), At(5), "ok");
        Check(!schedule.IsDue(At(11), anchor, state), "Inactive Wednesday due");
        Check(!schedule.IsDue(At(12), anchor, state), "Inactive Thursday due");
        Check(schedule.IsDue(At(18), anchor, state), "Second Wednesday not due");
        Check(schedule.IsDue(At(20), anchor, state), "Missed slot not recovered");
        Check(!schedule.IsDue(At(18).AddMinutes(10), anchor, state with { LastAttemptUtc = At(18) }), "Retry delay ignored");
        Check(!schedule.IsDue(At(4, 0).AddMinutes(-1), anchor, new(anchor, anchor, "ok")), "UTC date used instead of local date");

        var clock = new TestClock { Now = anchor };
        var storage = new SqliteSyncStateStore(connectionString);
        var calls = 0;
        Task<string> Sync(CancellationToken token) { calls++; return Task.FromResult("updated"); }
        var first = new ScheduledSync("test:catalog", storage, schedule, Sync, clock);
        await first.ExecuteAsync(false, default);
        Check(calls == 1, "Bootstrap missing");
        var restarted = new ScheduledSync("test:catalog", new SqliteSyncStateStore(connectionString), schedule, Sync, clock);
        await restarted.ExecuteAsync(false, default);
        Check(calls == 1, "Restart caused repeated sync");
        clock.Now = At(4);
        await restarted.ExecuteAsync(false, default);
        await restarted.ExecuteAsync(false, default);
        Check(calls == 2, "Daily slot repeated");
        await restarted.ExecuteAsync(true, default);
        Check(calls == 3, "Manual override failed");
        Check(await storage.GetAnchorAsync(default) == anchor, "Anchor shifted");
        var failed = new ScheduledSync("test:notes", storage, schedule, _ => throw new HttpRequestException("offline"), clock);
        await failed.ExecuteAsync(false, default);
        Check((await storage.GetAsync("test:notes", default)).LastSuccessUtc is null, "Failure marked successful");
        Check((await storage.GetAsync("test:catalog", default)).LastSuccessUtc is not null, "Independent catalog lost");

        var links = RiotPatchNotesSource.Parse("""<a href="/es-mx/news/game-updates/league-of-legends-patch-26-22-notes/">Notes</a>""");
        Check(links.Count == 1 && links[0].Host == "www.leagueoflegends.com", "Notes parsing failed");
        try { RiotPatchNotesSource.Parse("<html>Changed layout</html>"); throw new Exception("Empty notes accepted"); }
        catch (InvalidDataException) { }

        using var live = new LiveClient(new MissingGameHandler());
        try { await live.ReadAsync(default); throw new Exception("Missing game treated as active"); }
        catch (GameUnavailableException) { }
        using var cts = new CancellationTokenSource(); cts.Cancel();
        try { await live.ReadAsync(cts.Token); throw new Exception("Shutdown ignored"); }
        catch (OperationCanceledException) { }
        Console.WriteLine("PASS: alternating weeks, Wed/Thu slots, timezone, catch-up, persisted schedule, independent failures, absent game and cancellation.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class MissingGameHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
