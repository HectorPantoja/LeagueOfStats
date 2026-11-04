using LeagueOfStats.Domain.Matches;

namespace LeagueOfStats.Presentation.ViewModels;

public sealed record TeamPlayerRow(TeamPlayer Player, bool IsActivePlayer)
{
    public string Name => IsActivePlayer ? $"{Player.Name} (tú)" : Player.Name;
    public string Kda => $"{Player.Kills}/{Player.Deaths}/{Player.Assists}";
    public string State => Player.IsDead ? "Muerto" : "Vivo";
    public string Items => string.Join(", ", Player.Items.Select(i => i.Count > 1 ? $"{i.Name} ×{i.Count}" : i.Name));
}

