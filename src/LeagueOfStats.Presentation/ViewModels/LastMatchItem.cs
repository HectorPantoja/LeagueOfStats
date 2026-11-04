namespace LeagueOfStats.Presentation.ViewModels;

public sealed record LastMatchItem(int Id, string Name, int Count, string? ImageUrl)
{
    public string Caption => Count > 1 ? $"{Name} ×{Count}" : Name;
}
