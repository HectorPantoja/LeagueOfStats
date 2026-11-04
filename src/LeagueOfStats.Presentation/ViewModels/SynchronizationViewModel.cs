using LeagueOfStats.Application.Synchronization;

namespace LeagueOfStats.Presentation.ViewModels;

public sealed class SynchronizationViewModel(IScheduledSync synchronization) : ObservableViewModel
{
    private readonly object gate = new();
    private Task pending = Task.CompletedTask;
    private string status = "";

    public string Status { get => status; private set => Set(ref status, value); }
    public Task Pending { get { lock (gate) return pending; } }

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await RefreshAsync(false, token);
            await Task.Delay(TimeSpan.FromMinutes(1), token);
        }
    }

    public Task RefreshAsync(bool force, CancellationToken token)
    {
        lock (gate)
        {
            if (token.IsCancellationRequested) return Task.FromCanceled(token);
            if (!pending.IsCompleted) return pending;
            return pending = RefreshCoreAsync(force, token);
        }
    }

    private async Task RefreshCoreAsync(bool force, CancellationToken token)
    {
        try
        {
            var result = await Task.Run(() => synchronization.ExecuteAsync(force, token), token);
            token.ThrowIfCancellationRequested();
            Status = result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Status = $"No se pudo completar la revisión: {ex.Message}"; }
    }
}
