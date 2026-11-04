namespace LeagueOfStats.Application;

public interface IAppDiagnostics
{
    void Write(string code, string? sessionId = null, Exception? error = null);
}
