using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace AetherDPS.App.Services;

/// <summary>Win32: Fenster für Mausklicks durchlässig machen (Klicks gehen ins Spiel).</summary>
public static partial class ClickThrough
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x20;
    private const long WsExLayered = 0x80000;
    private const long WsExNoActivate = 0x8000000;

    public static void Set(Window window, bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return;

        long style = GetWindowLongPtrW(handle, GwlExStyle).ToInt64();
        long wanted = enabled ? style | WsExTransparent | WsExLayered : style & ~WsExTransparent;
        if (wanted != style)
            SetWindowLongPtrW(handle, GwlExStyle, new IntPtr(wanted));
    }

    /// <summary>
    /// Overlays nehmen nie den Fokus: Ein Klick darauf (Knöpfe, Ziehen im Platzierungsmodus) lässt das Spiel im
    /// Vordergrund, die Tastatur bleibt beim Spiel. Einmal nach dem Öffnen aufrufen.
    /// </summary>
    public static void NeverActivate(Window window)
    {
        if (!OperatingSystem.IsWindows()) return;
        void Apply()
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero) return;
            long style = GetWindowLongPtrW(handle, GwlExStyle).ToInt64();
            if ((style & WsExNoActivate) == 0)
                SetWindowLongPtrW(handle, GwlExStyle, new IntPtr(style | WsExNoActivate));
        }
        if (window.TryGetPlatformHandle() is not null) Apply();
        window.Opened += (_, _) => Apply();
    }

    /// <summary>
    /// Liegt der Mauszeiger über dem Control? Funktioniert auch, wenn das Fenster
    /// klick-durchlässig ist und selbst keine Mausereignisse mehr bekommt.
    /// </summary>
    public static bool IsCursorOver(Control control, int padding = 4)
    {
        if (!OperatingSystem.IsWindows() || !control.IsVisible || !GetCursorPos(out var cursor)) return false;
        var topLeft = control.PointToScreen(new Point(0, 0));
        var bottomRight = control.PointToScreen(new Point(control.Bounds.Width, control.Bounds.Height));
        return cursor.X >= topLeft.X - padding && cursor.X <= bottomRight.X + padding
            && cursor.Y >= topLeft.Y - padding && cursor.Y <= bottomRight.Y + padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out NativePoint point);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    private static partial IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
