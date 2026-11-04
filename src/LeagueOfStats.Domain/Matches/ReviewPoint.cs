namespace LeagueOfStats.Domain.Matches;

public sealed record ReviewPoint(double GameTime, int Level, double CurrentGold, int Kills, int Deaths,
    int Assists, int CreepScore, double? WardScore, bool IsDead, double? CurrentHealth, double? MaxHealth,
    IReadOnlyList<ReviewItem> Items);
