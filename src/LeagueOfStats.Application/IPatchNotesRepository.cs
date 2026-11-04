namespace LeagueOfStats.Application;

public interface IPatchNotesRepository
{
    Task SaveAsync(IReadOnlyList<Uri> links, CancellationToken token);
}
