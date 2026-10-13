using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Aion2Meter.App.Services;

/// <summary>Win32: Fenster für Mausklicks durchlässig machen (Klicks gehen ins Spiel).</summary>
public static partial class ClickThrough
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x20;
    private const long WsExLayered = 0x80000;

    public static void Set(Window window, bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return;

        long style = GetWindowLongPtrW(handle, GwlExStyle).ToInt64();
        style = enabled ? style | WsExTransparent | WsExLayered : style & ~WsExTransparent;
        SetWindowLongPtrW(handle, GwlExStyle, new IntPtr(style));
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    private static partial IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
