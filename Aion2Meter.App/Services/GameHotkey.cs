using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Aion2Meter.App.Services;

/// <summary>
/// STRG+W als Hotkey – aber NUR solange AION 2 (oder unser Overlay) im Vordergrund ist.
/// Sonst bleibt STRG+W frei, z. B. zum Schließen von Browser-Tabs.
/// Bewusst RegisterHotKey statt Tastatur-Hook: kein Mitlesen von Tastendrücken.
/// </summary>
public sealed partial class GameHotkey : IDisposable
{
    public const string DisplayName = "STRG+W";
    private const uint ModControl = 0x0002, ModNoRepeat = 0x4000, VkW = 0x57;
    private const uint WmHotkey = 0x0312, PmRemove = 0x0001;
    private const int HotkeyId = 0xA2;
    private static readonly string[] GameProcessNames = ["AION2"];

    private readonly Thread thread;
    private readonly int ownProcessId = Environment.ProcessId;
    private volatile bool running = true;

    /// <summary>Wird auf einem Hintergrund-Thread ausgelöst.</summary>
    public event EventHandler? Pressed;

    public GameHotkey()
    {
        thread = new Thread(Loop) { IsBackground = true, Name = "GameHotkey" };
        thread.Start();
    }

    private void Loop()
    {
        bool registered = false;
        var lastCheck = Stopwatch.StartNew();
        try
        {
            while (running)
            {
                if (lastCheck.ElapsedMilliseconds >= 250)
                {
                    lastCheck.Restart();
                    bool wanted = IsGameOrOverlayInForeground();
                    if (wanted && !registered) registered = RegisterHotKey(IntPtr.Zero, HotkeyId, ModControl | ModNoRepeat, VkW);
                    else if (!wanted && registered) registered = !UnregisterHotKey(IntPtr.Zero, HotkeyId);
                }

                while (PeekMessageW(out var msg, IntPtr.Zero, WmHotkey, WmHotkey, PmRemove))
                {
                    if (msg.message == WmHotkey && msg.wParam == HotkeyId)
                        Pressed?.Invoke(this, EventArgs.Empty);
                }
                Thread.Sleep(30);
            }
        }
        finally
        {
            if (registered) UnregisterHotKey(IntPtr.Zero, HotkeyId);
        }
    }

    private bool IsGameOrOverlayInForeground()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return false;
        GetWindowThreadProcessId(window, out uint pid);
        if (pid == ownProcessId) return true;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return GameProcessNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        running = false;
        thread.Join(500);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int ptX, ptY;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessageW(out Msg msg, IntPtr hWnd, uint filterMin, uint filterMax, uint remove);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
