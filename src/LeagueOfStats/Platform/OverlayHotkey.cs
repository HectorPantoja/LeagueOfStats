using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace LeagueOfStats.Platform;

/// <summary>Owns a global shortcut on the desktop UI thread, independently of overlay visibility.</summary>
public sealed class OverlayHotkey : IDisposable
{
    public const string Gesture = "Ctrl + Shift + Z";
    private const int HotkeyId = 0x4C53;
    private HwndSource? source;
    public event EventHandler? Pressed;

    public void Register(nint window)
    {
        if (source is not null) throw new InvalidOperationException("El atajo ya está registrado.");
        var target = HwndSource.FromHwnd(window)
            ?? throw new ArgumentException("Se requiere una ventana WPF inicializada.", nameof(window));
        // MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT prevents toggling repeatedly while held.
        if (!RegisterHotKey(window, HotkeyId, 0x0002 | 0x0004 | 0x4000, 0x5A))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        source = target;
        source.AddHook(WindowMessage);
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam == HotkeyId) // WM_HOTKEY
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }
        return 0;
    }

    public void Dispose()
    {
        if (source is null) return;
        UnregisterHotKey(source.Handle, HotkeyId);
        source.RemoveHook(WindowMessage);
        source = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
}
