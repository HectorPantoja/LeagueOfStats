using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using LeagueOfStats.Platform;

namespace LeagueOfStats;

public partial class OverlayWindow : Window
{
    private readonly WindowsOverlayHost host;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private nint handle;
    private HwndSource? source;
    private WindowBounds? previousBounds;
    private bool interactive;
    public string? PlatformError { get; private set; }

    public OverlayWindow(WindowsOverlayHost host)
    {
        InitializeComponent();
        this.host = host;
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(handle);
            source?.AddHook(WindowMessage);
            Apply(() => host.Configure(handle, interactive));
        };
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { previousBounds = null; RefreshPlacement(); timer.Start(); }
            else timer.Stop();
        };
        timer.Tick += (_, _) => RefreshPlacement();
        Closed += (_, _) => { timer.Stop(); source?.RemoveHook(WindowMessage); };
        MouseLeftButtonDown += (_, e) =>
        {
            if (interactive && e.LeftButton == MouseButtonState.Pressed) DragMove();
        };
    }

    public void SetInteractive(bool enabled)
    {
        interactive = enabled;
        if (handle != 0) Apply(() => host.Configure(handle, interactive));
    }

    public void ShowAboveGame()
    {
        ShowActivated = false;
        if (!IsVisible) Show();
        previousBounds = null;
        RefreshPlacement();
        if (handle != 0) Apply(() => host.Raise(handle));
    }

    private void RefreshPlacement()
    {
        if (handle == 0 || !IsVisible) return;
        Apply(() =>
        {
            if (host.TryGetGameBounds(out var bounds))
            {
                if (bounds != previousBounds)
                {
                    // Both API coordinates are device pixels, including on mixed-DPI monitors.
                    host.Position(handle, bounds.Left + 24, bounds.Top + 80);
                    previousBounds = bounds;
                }
                if (host.IsGameForeground) host.Raise(handle);
            }
        });
    }

    private void Apply(Action operation)
    {
        try { operation(); PlatformError = null; }
        catch (Win32Exception ex) { PlatformError = ex.Message; }
    }

    private static nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0021) // WM_MOUSEACTIVATE
        {
            handled = true;
            return new nint(3); // MA_NOACTIVATE: interacting must not take keyboard focus from LoL.
        }
        return 0;
    }
}
