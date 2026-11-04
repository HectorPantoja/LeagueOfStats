using LeagueOfStats.Application;
using LeagueOfStats.Application.Synchronization;
using LeagueOfStats.Domain.Synchronization;
using LeagueOfStats.Infrastructure.Persistence;
using LeagueOfStats.Infrastructure.Riot;
using LeagueOfStats.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LeagueOfStats.Bootstrapper;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLeagueOfStats(this IServiceCollection services, string? databaseDirectory = null)
    {
        services.AddSingleton(_ => new SqliteDatabase(databaseDirectory));
        services.AddSingleton<IAppDiagnostics>(_ => new FileDiagnostics(Path.Combine(databaseDirectory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeagueOfStats"), "logs")));
        services.AddSingleton<IApplicationInitializer>(sp => new DatabaseInitializer(ConnectionString(sp)));
        services.AddSingleton<ICatalogRepository>(sp => new SqliteCatalogRepository(ConnectionString(sp)));
        services.AddSingleton<ISampleRepository>(sp => new SqliteSampleRepository(ConnectionString(sp), sp.GetRequiredService<IAppDiagnostics>()));
        services.AddSingleton<IReviewRepository>(sp => new SqliteReviewRepository(ConnectionString(sp)));
        services.AddSingleton<CreateMatchReview>();
        services.AddSingleton<ReviewViewModel>();
        services.AddSingleton<LastMatchViewModel>();
        services.AddSingleton<IImageAssetCache>(sp => new DataDragonImageCache(
            new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) },
            Path.Combine(databaseDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LeagueOfStats"), "cache", "images"), sp.GetRequiredService<IAppDiagnostics>(), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ISyncStateStore>(sp => new SqliteSyncStateStore(ConnectionString(sp)));
        services.AddSingleton<IPatchNotesRepository>(sp => new SqlitePatchNotesRepository(ConnectionString(sp)));
        services.AddSingleton<TimeProvider>(_ => TimeProvider.System);
        services.AddSingleton<ISyncSchedule>(_ => new FortnightlySchedule(TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")));

        // Factories transfer ownership of disposable clients to the container.
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(20) });
        services.AddSingleton<ILiveClient>(sp => new LiveClient(sp.GetRequiredService<IAppDiagnostics>()));
        services.AddSingleton<DataDragonClient>();
        services.AddSingleton<IRiotCatalogClient>(sp => sp.GetRequiredService<DataDragonClient>());
        services.AddSingleton<IPatchVersionSource>(sp => sp.GetRequiredService<DataDragonClient>());
        services.AddSingleton<IPatchNotesSource, RiotPatchNotesSource>();
        services.AddSingleton<IMatchMonitor, MonitorMatch>();
        services.AddSingleton<RefreshCatalog>();
        services.AddSingleton<RefreshPatchNotes>();

        services.AddKeyedSingleton<IScheduledSync>(SynchronizationKeys.Catalog, (sp, _) =>
            new ScheduledSync("catalog:lan:es_MX", sp.GetRequiredService<ISyncStateStore>(),
                sp.GetRequiredService<ISyncSchedule>(), async token =>
                {
                    var result = await sp.GetRequiredService<RefreshCatalog>().ExecuteAsync("lan", "es_MX", token);
                    return $"Catálogo LAN: {result.Version}";
                }, sp.GetRequiredService<TimeProvider>()));
        services.AddKeyedSingleton<IScheduledSync>(SynchronizationKeys.Notes, (sp, _) =>
            new ScheduledSync("notes:es-mx", sp.GetRequiredService<ISyncStateStore>(),
                sp.GetRequiredService<ISyncSchedule>(), sp.GetRequiredService<RefreshPatchNotes>().ExecuteAsync,
                sp.GetRequiredService<TimeProvider>()));

        services.AddSingleton<LiveMonitorViewModel>();
        services.AddKeyedSingleton<SynchronizationViewModel>(SynchronizationKeys.Catalog, (sp, _) =>
            new SynchronizationViewModel(sp.GetRequiredKeyedService<IScheduledSync>(SynchronizationKeys.Catalog)));
        services.AddKeyedSingleton<SynchronizationViewModel>(SynchronizationKeys.Notes, (sp, _) =>
            new SynchronizationViewModel(sp.GetRequiredKeyedService<IScheduledSync>(SynchronizationKeys.Notes)));
        services.AddSingleton(sp => new MonitorViewModel(
            sp.GetRequiredService<LiveMonitorViewModel>(),
            sp.GetRequiredKeyedService<SynchronizationViewModel>(SynchronizationKeys.Catalog),
            sp.GetRequiredKeyedService<SynchronizationViewModel>(SynchronizationKeys.Notes),
            sp.GetRequiredService<IApplicationInitializer>(), sp.GetRequiredService<ReviewViewModel>(),
            sp.GetRequiredService<IAppDiagnostics>(), sp.GetRequiredService<LastMatchViewModel>()));
        return services;
    }

    private static string ConnectionString(IServiceProvider provider) => provider.GetRequiredService<SqliteDatabase>().ConnectionString;
}
