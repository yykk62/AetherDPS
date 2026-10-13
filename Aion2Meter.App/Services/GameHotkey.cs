using System.Diagnostics;
using System.Runtime.InteropServices;
using Aion2Meter.Engine;

namespace Aion2Meter.App.Services;

/// <summary>
/// Hotkey (Standard STRG+W, in den Einstellungen änderbar) – aber NUR solange AION 2 (oder unser Overlay) im Vordergrund ist.
/// Sonst bleibt die Taste frei (STRG+W schließt dann z. B. weiter Browser-Tabs).
/// Bewusst RegisterHotKey statt Tastatur-Hook: kein Mitlesen von Tastendrücken.
/// </summary>
public sealed partial class GameHotkey : IDisposable
{
    private const uint ModNoRepeat = 0x4000;
    private const uint WmHotkey = 0x0312, PmRemove = 0x0001;
    private const int HotkeyId = 0xA2;
    private const int DebounceMs = 1500;
    private static readonly string[] GameProcessNames = ["AION2"];

    private readonly Thread thread;
    private readonly int ownProcessId = Environment.ProcessId;
    private volatile bool running = true;

    /// <summary>Während in den Einstellungen ein neuer Hotkey aufgenommen wird.</summary>
    public static volatile bool Suspended;

    /// <summary>Wird auf einem Hintergrund-Thread ausgelöst.</summary>
    public event EventHandler? Pressed;

    private readonly MeterSettings settings;

    public GameHotkey(MeterSettings settings)
    {
        this.settings = settings;
        thread = new Thread(Loop) { IsBackground = true, Name = "GameHotkey" };
        thread.Start();
    }

    private void Loop()
    {
        bool registered = false;
        (uint Mods, uint Key) current = default;
        var lastCheck = Stopwatch.StartNew();
        var clock = Stopwatch.StartNew();
        long lastFiredMs = -DebounceMs;
        var notWanted = Stopwatch.StartNew();
        try
        {
            while (running)
            {
                if (lastCheck.ElapsedMilliseconds >= 250)
                {
                    lastCheck.Restart();
                    bool wanted = !Suspended && IsGameOrOverlayInForeground();
                    var combo = (settings.HotkeyModifiers, settings.HotkeyKey);
                    if (registered && combo != current)   // in den Einstellungen geändert
                        registered = !UnregisterHotKey(IntPtr.Zero, HotkeyId);
                    if (wanted && !registered)
                    {
                        current = combo;
                        registered = RegisterHotKey(IntPtr.Zero, HotkeyId, combo.Item1 | ModNoRepeat, combo.Item2);
                    }
                    else if (!wanted && registered)
                    {
                        // Erst nach 1 s ohne Spiel im Vordergrund freigeben – kurze Fokuswechsel
                        // (z. B. beim Einblenden des Overlays) sollen nicht neu registrieren.
                        if (Suspended || notWanted.ElapsedMilliseconds >= 1000) registered = !UnregisterHotKey(IntPtr.Zero, HotkeyId);
                    }
                    if (wanted) notWanted.Restart();
                }

                while (PeekMessageW(out var msg, IntPtr.Zero, WmHotkey, WmHotkey, PmRemove))
                {
                    if (msg.message == WmHotkey && msg.wParam == HotkeyId)
                    {
                        // Entprellen: Gedrückt gehaltene Tasten lösten sonst ein zweites Mal aus
                        // (aktiv → nach 0,5–1 s wieder Bereitschaft, im Log vom 13.10. gesehen).
                        if (clock.ElapsedMilliseconds - lastFiredMs >= DebounceMs)
                        {
                            lastFiredMs = clock.ElapsedMilliseconds;
                            Pressed?.Invoke(this, EventArgs.Empty);
                        }
                    }
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
