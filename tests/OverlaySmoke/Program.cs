using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LeagueOfStats;
using LeagueOfStats.Platform;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var game = new Window { Title = "LeagueOfStats overlay test", Width = 800, Height = 500, Left = 100, Top = 100, WindowStyle = WindowStyle.None };
        var host = new WindowsOverlayHost();
        var overlay = new OverlayWindow(host);
        try
        {
            game.Show();
            var before = GetForegroundWindow();
            overlay.ShowAboveGame();
            var handle = new WindowInteropHelper(overlay).Handle;
            if (overlay.PlatformError is not null) throw new Exception(overlay.PlatformError);
            var style = GetWindowLong(handle, -20);
            Check((style & 8) != 0, "Overlay is not topmost");
            Check((style & 0x08000000) != 0, "Overlay may activate");
            Check((style & 0x20) != 0, "Overlay intercepts game clicks");
            Check(GetForegroundWindow() == before, "Showing overlay stole focus");
            Check(WindowsOverlayHost.TryGetClientBounds(new WindowInteropHelper(game).Handle, out var bounds), "Cannot locate target window");
            host.Position(handle, bounds.Left + 24, bounds.Top + 80);
            Check(GetWindowRect(handle, out var rect) && rect.Left == bounds.Left + 24 && rect.Top == bounds.Top + 80, "Overlay positioned incorrectly");
            overlay.SetInteractive(true);
            Check((GetWindowLong(handle, -20) & 0x20) == 0, "Cannot unlock dragging");
            overlay.SetInteractive(false);
            overlay.Hide();
            overlay.ShowAboveGame();
            Check(overlay.IsVisible && (GetWindowLong(handle, -20) & 8) != 0, "Show/hide lost topmost");
            using var hotkey = new OverlayHotkey();
            var gameHandle = new WindowInteropHelper(game).Handle;
            hotkey.Register(gameHandle);
            var presses = 0;
            hotkey.Pressed += (_, _) =>
            {
                presses++;
                if (overlay.IsVisible) overlay.Hide(); else overlay.ShowAboveGame();
            };
            before = GetForegroundWindow();
            // Deliver native messages to the registered owner, including while minimized.
            SendMessage(gameHandle, 0x0312, 0x4C54, 0);
            Check(presses == 0, "Unrelated hotkey was handled");
            SendMessage(gameHandle, 0x0312, 0x4C53, 0);
            Check(presses == 1 && !overlay.IsVisible, "Hotkey did not hide overlay");
            SendMessage(gameHandle, 0x0312, 0x4C53, 0);
            Check(presses == 2 && overlay.IsVisible, "Hotkey did not restore hidden overlay");
            Check(GetForegroundWindow() == before, "Hotkey stole focus");
            game.WindowState = WindowState.Minimized;
            SendMessage(gameHandle, 0x0312, 0x4C53, 0);
            Check(presses == 3 && !overlay.IsVisible, "Minimized owner lost hotkey");
            hotkey.Dispose();
            hotkey.Dispose();
            SendMessage(gameHandle, 0x0312, 0x4C53, 0);
            Check(presses == 3, "Disposed hotkey still handles messages");
            using var replacement = new OverlayHotkey();
            replacement.Register(gameHandle);
            Console.WriteLine("PASS: global hotkey registration, routing, hidden/minimized windows and cleanup. Physical keyboard and exclusive fullscreen require manual validation.");
            Console.WriteLine("PASS: native topmost, no activation, click-through, placement, interaction mode and repeated show.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { overlay.Close(); game.Close(); app.Shutdown(); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rectangle);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);
}
