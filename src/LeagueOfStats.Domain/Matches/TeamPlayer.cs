namespace LeagueOfStats.Domain.Matches;

public sealed record TeamPlayer(
    string Name,
    string Champion,
    string Team,
    string Position,
    int Level,
    int Kills,
    int Deaths,
    int Assists,
    int CreepScore,
    bool IsDead,
    IReadOnlyList<LiveItem> Items)
{
    public double? WardScore { get; init; }
    public string ChampionId { get; init; } = "";
}
