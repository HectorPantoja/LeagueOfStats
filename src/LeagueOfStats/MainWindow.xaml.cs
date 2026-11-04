using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using LeagueOfStats.Platform;
using LeagueOfStats.Presentation.ViewModels;

namespace LeagueOfStats;

public partial class MainWindow : Window
{
    private readonly MonitorViewModel viewModel;
    private readonly OverlayWindow overlay;
    private readonly OverlayHotkey hotkey;
    private readonly System.Windows.Forms.NotifyIcon trayIcon;
    private bool closing;
    private bool closed;
    private bool overlaySuppressed;
    private bool matchVisible;
    private Task exportTask = Task.CompletedTask;

    public MainWindow(MonitorViewModel viewModel, OverlayWindow overlay, OverlayHotkey hotkey)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        this.overlay = overlay;
        this.hotkey = hotkey;
        var trayMenu = new System.Windows.Forms.ContextMenuStrip();
        trayMenu.Items.Add("Abrir LeagueOfStats", null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add("Salir", null, (_, _) => Close());
        trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "LeagueOfStats · Ctrl + Shift + Z muestra el overlay",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && !closing) Hide();
        };
        hotkey.Pressed += OnHotkeyPressed;
        SourceInitialized += (_, _) =>
        {
            try
            {
                hotkey.Register(new WindowInteropHelper(this).Handle);
                HotkeyStatus.Text = $"Atajo global: {OverlayHotkey.Gesture} · mostrar / ocultar overlay";
            }
            catch (Win32Exception ex)
            {
                HotkeyStatus.Text = $"No se pudo registrar {OverlayHotkey.Gesture}; puede estar ocupado por otra aplicación. Usa el botón. ({ex.NativeErrorCode})";
            }
        };
        DataContext = viewModel;
        overlay.DataContext = viewModel;
        viewModel.Live.PropertyChanged += OnLiveChanged;
        Loaded += (_, _) => viewModel.Start();
        Closing += OnClosing;
    }

    private async void RefreshPatch(object sender, RoutedEventArgs e) => await viewModel.RefreshAsync();
    private void OnCatalogImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Image image)
            image.SetCurrentValue(System.Windows.Controls.Image.SourceProperty, null);
        e.Handled = true;
    }

    private async void RefreshNotes(object sender, RoutedEventArgs e) => await viewModel.RefreshNotesAsync();
    private async void RefreshReview(object sender, RoutedEventArgs e) => await viewModel.RefreshReviewAsync();
    private async void GenerateReview(object sender, RoutedEventArgs e) => await viewModel.GenerateReviewAsync();
    private async void ExportReview(object sender, RoutedEventArgs e)
    {
        if (closing || !exportTask.IsCompleted) return;
        var content = viewModel.Review?.Prompt;
        if (string.IsNullOrWhiteSpace(content))
        {
            System.Windows.MessageBox.Show("Selecciona una partida cerrada y genera su resumen primero.", "Revisión de partida");
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "LeagueOfStats-review.txt", DefaultExt = ".txt", Filter = "Resumen para IA (*.txt)|*.txt"
        };
        if (dialog.ShowDialog(this) != true) return;
        try { exportTask = System.IO.File.WriteAllTextAsync(dialog.FileName, content); await exportTask; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        { System.Windows.MessageBox.Show("No se pudo guardar el archivo. Elige otra ubicación.", "Exportar revisión"); }
    }

    private void ToggleOverlay(object sender, RoutedEventArgs e)
        => ToggleOverlayVisibility();

    private void OnHotkeyPressed(object? sender, EventArgs e) => ToggleOverlayVisibility();

    private void ToggleOverlayVisibility()
    {
        if (closing) return;
        overlaySuppressed = overlay.IsVisible;
        if (overlaySuppressed) overlay.Hide(); else overlay.ShowAboveGame();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnLiveChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (closing || e.PropertyName != nameof(viewModel.Live.TeamPlayers)) return;
        var active = viewModel.Live.TeamPlayers.Count > 0;
        if (active && !matchVisible && !overlaySuppressed) overlay.ShowAboveGame();
        matchVisible = active;
    }

    private void ToggleOverlayInteraction(object sender, RoutedEventArgs e) =>
        overlay?.SetInteractive(sender is System.Windows.Controls.CheckBox { IsChecked: true });

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closed) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        trayIcon.Visible = false;
        trayIcon.Dispose();
        hotkey.Pressed -= OnHotkeyPressed;
        hotkey.Dispose();
        viewModel.Live.PropertyChanged -= OnLiveChanged;
        IsEnabled = false;
        try
        {
            await viewModel.DisposeAsync();
            try { await exportTask; } catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { }
        }
        finally
        {
            overlay.Close();
            closed = true;
            Close();
        }
    }
}




