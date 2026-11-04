namespace LeagueOfStats.Application;

public interface IPatchNotesSource
{
    Task<IReadOnlyList<Uri>> ReadLinksAsync(CancellationToken token);
}
