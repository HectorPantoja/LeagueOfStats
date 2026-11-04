using LeagueOfStats.Domain.Synchronization;
namespace LeagueOfStats.Application.Synchronization;

public sealed class ScheduledSync(
    string flow,
    ISyncStateStore store,
    ISyncSchedule schedule,
    Func<CancellationToken, Task<string>> synchronize,
    TimeProvider? timeProvider = null) : IScheduledSync
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<string> ExecuteAsync(bool force, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var state = await store.GetAsync(flow, token);
            var anchor = await store.GetAnchorAsync(token);
            var now = clock.GetUtcNow();
            if (!force && !schedule.IsDue(now, anchor, state))
            {
                var pending = state.LastAttemptUtc is not null &&
                    (state.LastSuccessUtc is null || state.LastAttemptUtc > state.LastSuccessUtc);
                return pending
                    ? $"{state.Summary ?? "Sin sincronización previa"} · revisión pendiente de reintento"
                    : state.Summary ?? "Esperando próxima revisión programada";
            }
            await store.AttemptAsync(flow, now, token);
            try
            {
                var summary = await synchronize(token);
                await store.CompleteAsync(flow, clock.GetUtcNow(), summary, token);
                return summary;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                return $"{state.Summary ?? "Sin sincronización previa"} · revisión pendiente: {ex.Message}";
            }
        }
        finally { gate.Release(); }
    }
}

