using System.Diagnostics;

namespace AetherDPS.App.Services;

/// <summary>Was die App selbst rund ums Update tun muss: Neustart, Aufräumen, nur eine Instanz.</summary>
public static class AppUpdate
{
    public const string UpdatedArg = "--updated";

    /// <summary>Nur die veröffentlichte Einzeldatei aktualisiert sich selbst – nicht die Builds aus dem Repo.</summary>
    public static bool CanSelfUpdate =>
#if SELF_UPDATE
        Environment.ProcessPath is not null;   // gesetzt von dotnet publish (AetherDPS.App.csproj)
#else
        false;
#endif

    public static string ExePath => Environment.ProcessPath!;

    /// <summary>Der Knopf „Jetzt neu starten“ – die App installiert und startet neu.</summary>
    public static event EventHandler? RestartRequested;

    public static void RequestRestart() => RestartRequested?.Invoke(null, EventArgs.Empty);

    public static void StartNewVersion() =>
        Process.Start(new ProcessStartInfo(ExePath, UpdatedArg) { UseShellExecute = false });

    /// <summary>Neustart ohne Update (z. B. neue Sprache) – wartet wie nach einem Update auf die alte Instanz.</summary>
    public const string RestartArg = "--restart";

    public static void StartAgain() =>
        Process.Start(new ProcessStartInfo(ExePath, RestartArg) { UseShellExecute = false });
}

/// <summary>
/// Nur ein Meter gleichzeitig (sonst zwei Tray-Symbole und doppelter Mitschnitt).
/// Ein zweiter Start holt stattdessen das Hauptfenster der laufenden Instanz nach vorn.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    // Bewusst der alte Name: Nach dem Update von „Aion2Meter“ (bis 0.5.0) muss die neue Version
    // auf genau diesen Mutex der alten warten.
    private const string MutexName = @"Local\Aion2Meter", ShowEventName = @"Local\Aion2Meter.Show";
    private readonly Mutex mutex;
    private readonly EventWaitHandle showEvent = new(false, EventResetMode.AutoReset, ShowEventName);
    private volatile bool disposed;

    private SingleInstance(Mutex mutex) => this.mutex = mutex;

    /// <param name="waitForPrevious">Nach einem Update: warten, bis die alte Version beendet ist.</param>
    public static SingleInstance? TryAcquire(bool waitForPrevious)
    {
        var mutex = new Mutex(false, MutexName);
        bool owned;
        try
        {
            owned = mutex.WaitOne(waitForPrevious ? TimeSpan.FromSeconds(20) : TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            owned = true;   // alte Instanz ist abgestürzt
        }
        if (owned) return new SingleInstance(mutex);

        if (EventWaitHandle.TryOpenExisting(ShowEventName, out var show))
        {
            show.Set();
            show.Dispose();
        }
        mutex.Dispose();
        return null;
    }

    public void ListenForShow(Action onShow) => new Thread(() =>
    {
        while (showEvent.WaitOne() && !disposed) onShow();
    }) { IsBackground = true, Name = "AetherDPS.Show" }.Start();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
        mutex.Dispose();
        showEvent.Set();
    }
}
