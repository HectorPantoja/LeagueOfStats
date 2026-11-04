namespace LeagueOfStats.Application;

public sealed class NullDiagnostics : IAppDiagnostics
{
    public static readonly NullDiagnostics Instance = new();
    public void Write(string code, string? sessionId = null, Exception? error = null) { }
}
