using LeagueOfStats.Application;
using LeagueOfStats.Domain.Matches;
namespace LeagueOfStats.Presentation.ViewModels;

public sealed class ReviewViewModel(IReviewRepository repository, CreateMatchReview create, IAppDiagnostics diagnostics)
    : ObservableViewModel
{
    private IReadOnlyList<MatchSummary> matches = Array.Empty<MatchSummary>();
    private MatchSummary? selected;
    private string status = "Actualiza el historial después de una partida.";
    private string summary = "";
    private string prompt = "";
    private bool busy;

    public IReadOnlyList<MatchSummary> Matches { get => matches; private set => Set(ref matches, value); }
    public MatchSummary? Selected
    {
        get => selected;
        set { if (selected != value) { Set(ref selected, value); Summary = ""; Prompt = ""; } }
    }
    public string Status { get => status; private set => Set(ref status, value); }
    public string Summary { get => summary; private set => Set(ref summary, value); }
    public string Prompt { get => prompt; private set => Set(ref prompt, value); }
    public bool Busy { get => busy; private set => Set(ref busy, value); }

    public Task Pending { get; private set; } = Task.CompletedTask;
    public Task RefreshAsync(CancellationToken token) => Busy ? Pending : Pending = RefreshCoreAsync(token);
    public Task GenerateAsync(CancellationToken token) => Busy ? Pending : Pending = GenerateCoreAsync(token);

    private async Task RefreshCoreAsync(CancellationToken token)
    {
        if (Busy) return;
        Busy = true;
        try
        {
            var rows = await Task.Run(() => repository.ListAsync(token), token);
            var previous = Selected?.SessionId;
            Matches = rows;
            Selected = rows.FirstOrDefault(m => m.SessionId == previous) ?? rows.FirstOrDefault();
            Status = rows.Count == 0 ? "Todavía no hay partidas capturadas." :
                "Selecciona una captura cerrada. La comparación usa hasta 10 partidas previas del mismo jugador, modo y mapa.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Status = "No se pudo cargar el historial. Reintenta; consulta el registro local si persiste."; diagnostics.Write("review.load.failed", error: ex); }
        finally { Busy = false; }
    }
    private async Task GenerateCoreAsync(CancellationToken token)
    {
        if (Busy || Selected is not { } match) return;
        if (!match.CanReview) { Status = "La captura sigue activa; espera al cierre y actualiza el historial."; return; }
        Busy = true;
        try
        {
            var package = await Task.Run(() => create.ExecuteAsync(match.SessionId, token), token);
            if (Selected?.SessionId != match.SessionId) return;
            Summary = package.Summary;
            Prompt = package.Prompt;
            Status = "Resumen guardado localmente. Puedes exportarlo y adjuntarlo a la IA que elijas.";
            diagnostics.Write("review.generated", match.SessionId);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Status = "No se pudo generar el resumen. Actualiza el historial y reintenta."; diagnostics.Write("review.generate.failed", error: ex); }
        finally { Busy = false; }
    }
}

