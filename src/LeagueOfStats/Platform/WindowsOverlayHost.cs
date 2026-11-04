using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LeagueOfStats.Platform;

// Only manipulates our overlay HWND. The game is inspected through Windows window metadata.
public sealed class WindowsOverlayHost
{
    private const int ExtendedStyle = -20;
    private const int NoActivate = 0x08000000, ToolWindow = 0x80, Transparent = 0x20;
    private const uint NoSize = 0x1, NoMove = 0x2, DoNotActivate = 0x10, FrameChanged = 0x20;
    // HWND_TOPMOST (-1) is a Z-order selector, not an arbitrary priority score.
    private static readonly nint Topmost = new(-1);
    private nint gameWindow;
    private DateTime nextDiscovery;

    public void Configure(nint window, bool interactive)
    {
        var style = GetWindowLong(window, ExtendedStyle) | NoActivate | ToolWindow;
        style = interactive ? style & ~Transparent : style | Transparent;
        Marshal.SetLastPInvokeError(0);
        if (SetWindowLong(window, ExtendedStyle, style) == 0 && Marshal.GetLastPInvokeError() != 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        Check(SetWindowPos(window, Topmost, 0, 0, 0, 0, NoSize | NoMove | DoNotActivate | FrameChanged));
    }

    public void Raise(nint window) => Check(SetWindowPos(window, Topmost, 0, 0, 0, 0, NoSize | NoMove | DoNotActivate));
    public void Position(nint window, int left, int top) => Check(SetWindowPos(window, Topmost, left, top, 0, 0, NoSize | DoNotActivate));
    public bool IsGameForeground => gameWindow != 0 && GetForegroundWindow() == gameWindow;

    public bool TryGetGameBounds(out WindowBounds bounds)
    {
        bounds = default;
        if (gameWindow == 0 || !IsWindow(gameWindow))
        {
            if (DateTime.UtcNow < nextDiscovery) return false;
            nextDiscovery = DateTime.UtcNow.AddSeconds(2);
            gameWindow = FindGameWindow();
        }
        return gameWindow != 0 && IsWindowVisible(gameWindow) && !IsIconic(gameWindow) && TryGetClientBounds(gameWindow, out bounds);
    }

    public static bool TryGetClientBounds(nint window, out WindowBounds bounds)
    {
        bounds = default;
        if (!GetClientRect(window, out var rectangle)) return false;
        var origin = new Point();
        if (!ClientToScreen(window, ref origin)) return false;
        bounds = new(origin.X, origin.Y, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static nint FindGameWindow()
    {
        var processes = Process.GetProcessesByName("League of Legends");
        try
        {
            var ids = processes.Select(p => (uint)p.Id).ToHashSet();
            nint found = 0;
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out var processId);
                if (!ids.Contains(processId) || !IsWindowVisible(window) || !TryGetClientBounds(window, out var area) || area.Width < 300 || area.Height < 200)
                    return true;
                found = window;
                return false;
            }, 0);
            return found;
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    private static void Check(bool success) { if (!success) throw new Win32Exception(Marshal.GetLastPInvokeError()); }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    private delegate bool EnumWindowsCallback(nint window, nint state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, nint state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect rectangle);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint window, ref Point point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetWindowLong(nint window, int index, int value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
