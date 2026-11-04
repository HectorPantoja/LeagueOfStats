namespace LeagueOfStats.Domain.Matches;

public sealed class ActiveTeamSnapshot
{
    public TeamPlayer ActivePlayer { get; }
    public IReadOnlyList<TeamPlayer> Players { get; }
    public double? CurrentHealth { get; }
    public double? MaxHealth { get; }

    public ActiveTeamSnapshot(TeamPlayer activePlayer, IEnumerable<TeamPlayer> players,
        double? currentHealth, double? maxHealth)
    {
        ArgumentNullException.ThrowIfNull(activePlayer);
        var members = players.ToArray();
        if (string.IsNullOrWhiteSpace(activePlayer.Team) || members.Length == 0 ||
            members.Any(p => p.Team != activePlayer.Team) ||
            members.Count(p => p.Name == activePlayer.Name) != 1)
            throw new ArgumentException("Snapshot must contain the active player and only their team.");
        Players = Array.AsReadOnly(members.Select(p => p with { Items = Array.AsReadOnly(p.Items.ToArray()) }).ToArray());
        ActivePlayer = Players.Single(p => p.Name == activePlayer.Name);
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
    }
}
