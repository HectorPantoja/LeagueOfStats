using LeagueOfStats.Platform;
using System.Windows;
using LeagueOfStats.Bootstrapper;
using Microsoft.Extensions.DependencyInjection;

namespace LeagueOfStats;

public partial class App : System.Windows.Application
{
    private ServiceProvider? services;
    private Task? shutdownTask;
    private Mutex? instanceMutex;
    private bool ownsMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            instanceMutex = new Mutex(false, "Local\\LeagueOfStats.Desktop");
            try { ownsMutex = instanceMutex.WaitOne(0); }
            catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex)
            {
                System.Windows.MessageBox.Show("LeagueOfStats ya está abierto. Puedes restaurarlo desde la bandeja del sistema.", "LeagueOfStats");
                Shutdown();
                return;
            }
            var registrations = new ServiceCollection();
            registrations.AddLeagueOfStats();
            registrations.AddSingleton<WindowsOverlayHost>();
            registrations.AddSingleton<OverlayHotkey>();
            registrations.AddSingleton<OverlayWindow>();
            registrations.AddSingleton<MainWindow>();
            services = registrations.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            MainWindow = services.GetRequiredService<MainWindow>();
            MainWindow.Closed += OnMainWindowClosed;
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"No se pudo iniciar LeagueOfStats: {ex.Message}", "LeagueOfStats");
            await StopAsync(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsMutex) instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private async void OnMainWindowClosed(object? sender, EventArgs e) => await StopAsync(0);

    private Task StopAsync(int exitCode) => shutdownTask ??= StopCoreAsync(exitCode);

    private async Task StopCoreAsync(int exitCode)
    {
        try
        {
            // Await async-only services before WPF stops its dispatcher.
            if (services is not null) await services.DisposeAsync();
        }
        catch (Exception ex)
        {
            exitCode = 1;
            System.Windows.MessageBox.Show($"No se pudo completar el cierre: {ex.Message}", "LeagueOfStats");
        }
        finally { Shutdown(exitCode); }
    }
}

