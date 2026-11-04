namespace LeagueOfStats.Domain.Matches;

public sealed record PlayerSample
{
    public double GameTime { get; }
    public int Level { get; }
    public double Gold { get; }
    public ActiveTeamSnapshot? TeamSnapshot { get; init; }
    public IReadOnlyList<MatchEvent> Events { get; init; } = Array.Empty<MatchEvent>();
    public string GameMode { get; init; } = "UNKNOWN";
    public int MapNumber { get; init; }
    public bool EventsAvailable { get; init; } = true;

    public PlayerSample(double gameTime, int level, double gold)
    {
        if (!double.IsFinite(gameTime) || gameTime < 0 || gameTime > TimeSpan.MaxValue.TotalSeconds)
            throw new ArgumentOutOfRangeException(nameof(gameTime));
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
        if (!double.IsFinite(gold) || gold < 0) throw new ArgumentOutOfRangeException(nameof(gold));
        GameTime = gameTime;
        Level = level;
        Gold = gold;
    }
}
