namespace LeagueOfStats.Application;

public interface IApplicationInitializer
{
    Task InitializeAsync(CancellationToken token);
}
