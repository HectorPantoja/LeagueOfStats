using LeagueOfStats.Application;
using LeagueOfStats.Application.Synchronization;
using LeagueOfStats.Domain.Matches;
using LeagueOfStats.Presentation.ViewModels;

internal static class PresentationChecks
{
    public static async Task RunAsync()
    {
        var player = new TeamPlayer("Me#LAN", "Ahri", "ORDER", "MIDDLE", 8, 1, 2, 3, 80, false, []);
        var sample = new PlayerSample(500, 8, 1200)
        {
            TeamSnapshot = new ActiveTeamSnapshot(player, [player], 700, 1000)
        };
        var available = true;
        var live = new LiveMonitorViewModel(new MonitorStub(_ => available
            ? Task.FromResult(sample)
            : throw new GameUnavailableException(new Exception("No game"))));
        Check(await live.CaptureAsync(default) == TimeSpan.FromSeconds(2), "Active polling interval changed");
        Check(live.TeamPlayers.Count == 1 && live.ActivePlayer.Contains("Ahri"), "Active projection missing");
        available = false;
        Check(await live.CaptureAsync(default) == TimeSpan.FromSeconds(15), "Idle polling interval changed");
        Check(live.TeamPlayers.Count == 0 && live.ActivePlayer == "" && live.Stats == "", "Stale player data retained");

        var release = Signal();
        var entered = Signal();
        var calls = 0;
        var sync = new SynchronizationViewModel(new SyncStub(async (_, token) =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return "synced";
        }));
        var first = sync.RefreshAsync(false, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = sync.RefreshAsync(true, default);
        Check(ReferenceEquals(first, second), "Concurrent refreshes were not coalesced");
        release.SetResult();
        await Task.WhenAll(first, second);
        Check(calls == 1 && sync.Status == "synced", "Sync ran more than once");

        var initEntered = Signal();
        var initCancelled = false;
        var initializing = new MonitorViewModel(live, sync, sync, new InitializerStub(async token =>
        {
            initEntered.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { initCancelled = true; throw; }
        }));
        initializing.Start();
        initializing.Start();
        await initEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var close = initializing.DisposeAsync().AsTask();
        var closeAgain = initializing.DisposeAsync().AsTask();
        Check(ReferenceEquals(close, closeAgain), "Repeated disposal did not await the same shutdown");
        await close.WaitAsync(TimeSpan.FromSeconds(5));
        Check(initCancelled, "Initialization was not cancelled");

        var matchEntered = Signal();
        var catalogEntered = Signal();
        var notesEntered = Signal();
        var cancelled = 0;
        async Task Block(TaskCompletionSource signal, CancellationToken token)
        {
            signal.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { Interlocked.Increment(ref cancelled); throw; }
        }
        var running = new MonitorViewModel(
            new LiveMonitorViewModel(new MonitorStub(async token => { await Block(matchEntered, token); return sample; })),
            new SynchronizationViewModel(new SyncStub(async (_, token) => { await Block(catalogEntered, token); return "catalog"; })),
            new SynchronizationViewModel(new SyncStub(async (_, token) => { await Block(notesEntered, token); return "notes"; })),
            new InitializerStub(_ => Task.CompletedTask));
        running.Start();
        await Task.WhenAll(matchEntered.Task, catalogEntered.Task, notesEntered.Task).WaitAsync(TimeSpan.FromSeconds(5));
        await running.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Check(cancelled == 3, "Shutdown left a background operation running");
        Check(running.Catalog.Status == "" && running.Notes.Status == "", "Cancellation reported as success");

        var references = typeof(MonitorViewModel).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Check(!references.Any(n => n.Contains("Infrastructure") || n.Contains("Sqlite") || n == "PresentationFramework"), "Presentation depends on concrete infrastructure");
        Console.WriteLine("PASS: presentation substitution, stale-data clearing, refresh coalescing, initialization cancellation and coordinated shutdown.");
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class MonitorStub(Func<CancellationToken, Task<PlayerSample>> capture) : IMatchMonitor
    {
        public Task<PlayerSample> CaptureAsync(CancellationToken token) => capture(token);
        public Task ReportUnavailableAsync(CancellationToken token) => Task.CompletedTask;
    }
    private sealed class SyncStub(Func<bool, CancellationToken, Task<string>> execute) : IScheduledSync
    {
        public Task<string> ExecuteAsync(bool force, CancellationToken token) => execute(force, token);
    }
    private sealed class InitializerStub(Func<CancellationToken, Task> initialize) : IApplicationInitializer
    {
        public Task InitializeAsync(CancellationToken token) => initialize(token);
    }
}
