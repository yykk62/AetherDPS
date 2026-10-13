using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Avalonia;

namespace Aion2Meter.App.Services;

/// <summary>
/// Screenshot des Bildschirms, auf dem AION 2 läuft (sonst des Hauptbildschirms).
/// GDI-Aufnahme: funktioniert im randlosen Fenstermodus; im exklusiven Vollbild kann
/// das Bild schwarz sein – dann im Spiel auf „Randloses Fenster“ stellen.
/// </summary>
public static partial class ScreenCapture
{
    public sealed record Shot(Avalonia.Media.Imaging.Bitmap Image, PixelRect Monitor);

    public static Shot? CaptureGameMonitor(IntPtr foreground)
    {
        var window = FindGameWindow() ?? foreground;
        if (!TryGetMonitorRect(window, out var rect)) return null;

        using var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
            g.CopyFromScreen(rect.X, rect.Y, 0, 0, new System.Drawing.Size(rect.Width, rect.Height));

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Bmp);   // BMP: schneller als PNG, Bild bleibt nur im Speicher
        stream.Position = 0;
        return new Shot(new Avalonia.Media.Imaging.Bitmap(stream), rect);
    }

    private static IntPtr? FindGameWindow()
    {
        foreach (var process in Process.GetProcessesByName("AION2"))
        {
            using (process)
            {
                if (process.MainWindowHandle != IntPtr.Zero) return process.MainWindowHandle;
            }
        }
        return null;
    }

    private static bool TryGetMonitorRect(IntPtr window, out PixelRect rect)
    {
        var monitor = MonitorFromWindow(window, 2 /* MONITOR_DEFAULTTONEAREST */);
        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfoW(monitor, ref info))
        {
            rect = default;
            return false;
        }
        rect = new PixelRect(info.rcMonitor.Left, info.rcMonitor.Top,
            info.rcMonitor.Right - info.rcMonitor.Left, info.rcMonitor.Bottom - info.rcMonitor.Top);
        return rect.Width > 0 && rect.Height > 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public NativeRect rcMonitor;
        public NativeRect rcWork;
        public uint dwFlags;
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);
}
