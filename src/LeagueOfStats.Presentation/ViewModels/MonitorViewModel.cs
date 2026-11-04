using LeagueOfStats.Application;

namespace LeagueOfStats.Presentation.ViewModels;

// Coordinates startup and shutdown. Child view models own their own state and polling.
public sealed class MonitorViewModel(
    LiveMonitorViewModel live,
    SynchronizationViewModel catalog,
    SynchronizationViewModel notes,
    IApplicationInitializer initializer, ReviewViewModel? review = null, IAppDiagnostics? diagnostics = null,
    LastMatchViewModel? lastMatch = null) : IAsyncDisposable
{
    private readonly CancellationTokenSource shutdown = new();
    private Task run = Task.CompletedTask;
    private Task? disposeTask;
    private bool started;
    private bool stopping;
    private bool ready;
    private readonly IAppDiagnostics log = diagnostics ?? NullDiagnostics.Instance;

    public LiveMonitorViewModel Live { get; } = live;
    public SynchronizationViewModel Catalog { get; } = catalog;
    public SynchronizationViewModel Notes { get; } = notes;
    public ReviewViewModel? Review { get; } = review;
    public LastMatchViewModel? LastMatch { get; } = lastMatch;

    // Lifecycle and UI commands are invoked from the UI thread.
    public void Start()
    {
        if (started || stopping) return;
        started = true;
        run = RunAsync(shutdown.Token);
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            await Task.Run(() => initializer.InitializeAsync(token), token);
            token.ThrowIfCancellationRequested();
            ready = true;
            log.Write("application.ready");
            if (Review is not null) await Review.RefreshAsync(token);
            await Task.WhenAll(Live.RunAsync(token), Catalog.RunAsync(token), Notes.RunAsync(token),
                LastMatch?.RunAsync(token) ?? Task.CompletedTask);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { log.Write("application.failed", error: ex); Live.ReportInitializationError(ex); }
    }

    public Task RefreshAsync(bool force = true) => !ready || stopping
        ? Task.CompletedTask : Catalog.RefreshAsync(force, shutdown.Token);
    public Task RefreshNotesAsync(bool force = true) => !ready || stopping
        ? Task.CompletedTask : Notes.RefreshAsync(force, shutdown.Token);
    public Task RefreshReviewAsync() => !ready || stopping || Review is null
        ? Task.CompletedTask : Review.RefreshAsync(shutdown.Token);
    public Task GenerateReviewAsync() => !ready || stopping || Review is null
        ? Task.CompletedTask : Review.GenerateAsync(shutdown.Token);

    public ValueTask DisposeAsync()
    {
        disposeTask ??= StopAsync();
        return new ValueTask(disposeTask);
    }

    private async Task StopAsync()
    {
        stopping = true;
        shutdown.Cancel();
        try { await Task.WhenAll(run, Catalog.Pending, Notes.Pending, Review?.Pending ?? Task.CompletedTask); }
        finally { log.Write("application.stopped"); shutdown.Dispose(); }
    }
}
