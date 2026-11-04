using LeagueOfStats.Application;
using LeagueOfStats.Application.Synchronization;
using LeagueOfStats.Bootstrapper;
using LeagueOfStats.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

internal static class DependencyInjectionChecks
{
    public static async Task RunAsync(string directory)
    {
        var services = new ServiceCollection().AddLeagueOfStats(directory);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var root = provider.GetRequiredService<MonitorViewModel>();
        Check(ReferenceEquals(root, provider.GetRequiredService<MonitorViewModel>()), "Root lifetime mismatch");
        Check(ReferenceEquals(root.Live, provider.GetRequiredService<LiveMonitorViewModel>()), "Live view model not injected");
        Check(ReferenceEquals(root.Catalog, provider.GetRequiredKeyedService<SynchronizationViewModel>(SynchronizationKeys.Catalog)), "Wrong catalog registration");
        Check(ReferenceEquals(root.Notes, provider.GetRequiredKeyedService<SynchronizationViewModel>(SynchronizationKeys.Notes)), "Wrong notes registration");
        Check(!ReferenceEquals(root.Catalog, root.Notes), "Sync view model states shared");
        Check(!ReferenceEquals(provider.GetRequiredKeyedService<IScheduledSync>(SynchronizationKeys.Catalog),
            provider.GetRequiredKeyedService<IScheduledSync>(SynchronizationKeys.Notes)), "Sync jobs shared");
        Check(ReferenceEquals(provider.GetRequiredService<IRiotCatalogClient>(), provider.GetRequiredService<IPatchVersionSource>()), "Data Dragon aliases not shared");
        await provider.GetRequiredService<IApplicationInitializer>().InitializeAsync(default);
        Check(File.Exists(Path.Combine(directory, "leagueofstats.db")), "Initializer not wired to configured database");

        var http = provider.GetRequiredService<HttpClient>();
        var live = provider.GetRequiredService<ILiveClient>();
        await provider.DisposeAsync();
        try { await http.GetAsync("https://example.invalid/"); throw new Exception("HTTP client was not disposed"); }
        catch (ObjectDisposedException) { }
        try { await live.ReadAsync(default); throw new Exception("Live client was not disposed"); }
        catch (ObjectDisposedException) { }
        await root.DisposeAsync(); // The window and container can both request shutdown safely.
        Console.WriteLine("PASS: DI graph, keyed services, singleton lifetimes, database configuration and async disposal.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
