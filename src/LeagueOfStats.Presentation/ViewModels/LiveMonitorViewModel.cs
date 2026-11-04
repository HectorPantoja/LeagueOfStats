using LeagueOfStats.Application;

namespace LeagueOfStats.Presentation.ViewModels;

public sealed class LiveMonitorViewModel(IMatchMonitor monitor) : ObservableViewModel
{
    private string status = "Iniciando…";
    private string stats = "";
    private string activePlayer = "";
    private IReadOnlyList<TeamPlayerRow> teamPlayers = Array.Empty<TeamPlayerRow>();

    public string Status { get => status; private set => Set(ref status, value); }
    public string Stats { get => stats; private set => Set(ref stats, value); }
    public string ActivePlayer { get => activePlayer; private set => Set(ref activePlayer, value); }
    public IReadOnlyList<TeamPlayerRow> TeamPlayers { get => teamPlayers; private set => Set(ref teamPlayers, value); }

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var delay = await CaptureAsync(token);
            await Task.Delay(delay, token);
        }
    }

    public async Task<TimeSpan> CaptureAsync(CancellationToken token)
    {
        try
        {
            var sample = await Task.Run(() => monitor.CaptureAsync(token), token);
            token.ThrowIfCancellationRequested();
            Status = "Partida detectada · jugador activo y equipo aliado";
            Stats = $"{TimeSpan.FromSeconds(sample.GameTime):hh\\:mm\\:ss} · Nivel {sample.Level} · Oro {sample.Gold:0}";
            if (sample.TeamSnapshot is { } snapshot)
            {
                var player = snapshot.ActivePlayer;
                var health = snapshot.CurrentHealth is { } hp && snapshot.MaxHealth is { } max
                    ? $" · Vida {hp:0}/{max:0}" : "";
                ActivePlayer = $"{player.Name} · {player.Champion} · {player.Team}{health}";
                TeamPlayers = snapshot.Players.Select(p => new TeamPlayerRow(p, p.Name == player.Name)).ToArray();
            }
            else { ActivePlayer = ""; TeamPlayers = Array.Empty<TeamPlayerRow>(); }
            return TimeSpan.FromSeconds(2);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (GameUnavailableException) { await ReportFailureAsync("Esperando partida", token); }
        catch (Exception) { await ReportFailureAsync("Error de monitoreo; se reintentará. Consulta el registro local si persiste.", token); }
        return TimeSpan.FromSeconds(15);
    }

    private async Task ReportFailureAsync(string message, CancellationToken token)
    {
        try
        {
            await Task.Run(() => monitor.ReportUnavailableAsync(token), token);
            Clear(message);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { Clear("No se pudo actualizar la captura; se reintentará. Consulta el registro local si persiste."); }
    }

    public void ReportInitializationError(Exception error) => Clear($"No se pudo iniciar: {error.Message}");

    private void Clear(string message)
    {
        Status = message;
        Stats = "";
        ActivePlayer = "";
        TeamPlayers = Array.Empty<TeamPlayerRow>();
    }
}

