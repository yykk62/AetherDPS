using System.Diagnostics;
using System.Runtime.InteropServices;
using Aion2Meter.Engine;

namespace Aion2Meter.App.Services;

/// <summary>
/// Hotkeys (Meter: Standard STRG+W, Screenshot: Standard STRG+S; in den Einstellungen
/// änderbar) – aber NUR solange AION 2 (oder unser Overlay/Hauptfenster) im Vordergrund ist.
/// Sonst bleiben die Tasten frei (STRG+W schließt dann weiter Browser-Tabs, STRG+S speichert).
/// Bewusst RegisterHotKey statt Tastatur-Hook: kein Mitlesen von Tastendrücken.
/// </summary>
public sealed partial class GameHotkey : IDisposable
{
    private const uint ModNoRepeat = 0x4000;
    private const uint WmHotkey = 0x0312, PmRemove = 0x0001;
    private static readonly string[] GameProcessNames = ["AION2"];

    private sealed class Slot(int id, int debounceMs, Func<(uint Mods, uint Key)> combo, Action<IntPtr> fire)
    {
        public int Id { get; } = id;
        public int DebounceMs { get; } = debounceMs;
        public Func<(uint Mods, uint Key)> Combo { get; } = combo;
        public Action<IntPtr> Fire { get; } = fire;
        public bool Registered { get; set; }
        public (uint Mods, uint Key) Current { get; set; }
        public long LastFiredMs { get; set; } = -100_000;
    }

    private readonly Thread thread;
    private readonly int ownProcessId = Environment.ProcessId;
    private readonly Slot[] slots;
    private volatile bool running = true;

    /// <summary>Während in den Einstellungen ein neuer Hotkey aufgenommen wird.</summary>
    public static volatile bool Suspended;

    /// <summary>Während der geführten Aufnahme: F8 = Schritt erledigt, F9 = Schritt überspringen.</summary>
    public static volatile bool MarkerEnabled;

    public event EventHandler? MarkerPressed;
    public event EventHandler? SkipPressed;

    /// <summary>Meter starten/stoppen. Wird auf einem Hintergrund-Thread ausgelöst.</summary>
    public event EventHandler? Pressed;

    /// <summary>Screenshot. Argument = Fenster im Vordergrund beim Drücken (meist AION 2).</summary>
    public event EventHandler<IntPtr>? ScreenshotPressed;

    public GameHotkey(MeterSettings settings)
    {
        slots =
        [
            // Entprellen 1,5 s: gedrückt gehaltenes STRG+W schaltete sonst sofort wieder aus (Log 13.10.)
            new Slot(0xA2, 1500, () => (settings.HotkeyModifiers, settings.HotkeyKey),
                _ => Pressed?.Invoke(this, EventArgs.Empty)),
            new Slot(0xA3, 300, () => settings.ScreenshotEnabled ? (settings.ScreenshotModifiers, settings.ScreenshotKey) : (0u, 0u),
                fg => ScreenshotPressed?.Invoke(this, fg)),
            new Slot(0xA4, 800, () => MarkerEnabled ? (0u, 0x77u) : (0u, 0u),   // F8
                _ => MarkerPressed?.Invoke(this, EventArgs.Empty)),
            new Slot(0xA5, 800, () => MarkerEnabled ? (0u, 0x78u) : (0u, 0u),   // F9
                _ => SkipPressed?.Invoke(this, EventArgs.Empty)),
        ];
        thread = new Thread(Loop) { IsBackground = true, Name = "GameHotkey" };
        thread.Start();
    }

    private void Loop()
    {
        var clock = Stopwatch.StartNew();
        long lastCheck = -1000;
        var notWanted = Stopwatch.StartNew();
        try
        {
            while (running)
            {
                if (clock.ElapsedMilliseconds - lastCheck >= 250)
                {
                    lastCheck = clock.ElapsedMilliseconds;
                    bool wanted = !Suspended && IsGameOrOverlayInForeground();
                    if (wanted) notWanted.Restart();
                    // Erst nach 1 s ohne Spiel im Vordergrund freigeben – kurze Fokuswechsel
                    // (z. B. beim Einblenden des Overlays) sollen nicht neu registrieren.
                    bool release = Suspended || notWanted.ElapsedMilliseconds >= 1000;
                    foreach (var slot in slots) Update(slot, wanted, release);
                }

                while (PeekMessageW(out var msg, IntPtr.Zero, WmHotkey, WmHotkey, PmRemove))
                {
                    var slot = slots.FirstOrDefault(s => s.Id == (int)msg.wParam);
                    if (slot is null || clock.ElapsedMilliseconds - slot.LastFiredMs < slot.DebounceMs) continue;
                    slot.LastFiredMs = clock.ElapsedMilliseconds;
                    slot.Fire(GetForegroundWindow());
                }
                Thread.Sleep(30);
            }
        }
        finally
        {
            foreach (var slot in slots.Where(s => s.Registered)) UnregisterHotKey(IntPtr.Zero, slot.Id);
        }
    }

    private static void Update(Slot slot, bool wanted, bool release)
    {
        var combo = slot.Combo();
        if (slot.Registered && combo != slot.Current)   // in den Einstellungen geändert
            slot.Registered = !UnregisterHotKey(IntPtr.Zero, slot.Id);
        if (wanted && !slot.Registered && combo.Key != 0)
        {
            slot.Current = combo;
            slot.Registered = RegisterHotKey(IntPtr.Zero, slot.Id, combo.Mods | ModNoRepeat, combo.Key);
        }
        else if (!wanted && slot.Registered && release)
        {
            slot.Registered = !UnregisterHotKey(IntPtr.Zero, slot.Id);
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
