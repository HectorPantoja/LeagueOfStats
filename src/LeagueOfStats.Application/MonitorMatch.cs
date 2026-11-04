using LeagueOfStats.Domain.Matches;
namespace LeagueOfStats.Application;

public sealed class MonitorMatch(ILiveClient client, ISampleRepository repository,
    TimeProvider? timeProvider = null, IAppDiagnostics? diagnostics = null) : IMatchMonitor, IDisposable
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly IAppDiagnostics log = diagnostics ?? NullDiagnostics.Instance;
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<PlayerSample> CaptureAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var sample = await client.ReadAsync(token);
            var state = await repository.RecordAsync(sample, clock.GetUtcNow(), token);
            log.Write("capture.saved", state.SessionId);
            return sample;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (GameUnavailableException) { throw; }
        catch (Exception ex) { log.Write("capture.failed", error: ex); throw; }
        finally { gate.Release(); }
    }

    public async Task ReportUnavailableAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { await repository.MarkUnavailableAsync(clock.GetUtcNow(), token); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { log.Write("capture.disconnect.failed", error: ex); throw; }
        finally { gate.Release(); }
    }

    public void Dispose() => gate.Dispose();
}

