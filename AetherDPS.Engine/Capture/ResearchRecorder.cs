using System.Globalization;
using System.Text;
using AionDpsMeter.Services.PacketCapture;

namespace AetherDPS.Engine.Capture;

/// <summary>Ein Schritt der geführten Aufnahme: was der Spieler tun soll und wozu.</summary>
public sealed record ResearchStep(string Key, string Task, string Why, bool AsksText = false);

/// <summary>
/// Geführte Aufnahme: schreibt ALLE Rohdaten in einen Mitschnitt (nicht nur 5 Minuten) und setzt
/// bei jedem erledigten Schritt eine Marke. So lässt sich hinterher genau sagen, welches Paket
/// beim Dungeon-Betreten, beim Tod, bei Beute usw. kam. Bleibt lokal – enthält Spielernamen.
/// Marken stehen als Kommentar im Mitschnitt: <c># MARK|ZEIT|SCHLÜSSEL|ergebnis|Text</c>.
/// Auswertung: Die Marke kommt NACH der Aktion, oft einige Sekunden bis Minuten später
/// (der Spieler drückt F8 hinterher). Also im Fenster zwischen vorheriger und dieser Marke suchen.
/// </summary>
public sealed class ResearchRecorder
{
    public static readonly IReadOnlyList<ResearchStep> Steps =
    [
        new("server", "Auf welchem Server spielst du? Trag den Namen ein und drück „Erledigt“.", "Server-Anzeige für EU", AsksText: true),
        new("relog", "Logge aus bis zur Charakterauswahl und wieder ein.", "Charakter und Server beim Login"),
        new("teleport", "Teleportiere dich an einen anderen Ort.", "Gebietswechsel erkennen"),
        new("channel", "Wechsle den Kanal.", "Kanal-Anzeige"),
        new("food", "Iss etwas oder benutze eine Rolle (ein Buff mit Laufzeit).", "Warnung, wenn ein Buff ausläuft"),
        new("farm", "Töte etwa eine Minute lang normale Monster.", "Erfahrung pro Stunde"),
        new("loot", "Hebe Beute und Kinah auf.", "Beute-Log, Kinah pro Stunde"),
        new("party", "Lade jemanden in eine Gruppe ein oder tritt einer bei.", "Gruppe, Server der Mitspieler"),
        new("dungeon_in", "Betritt einen Dungeon.", "Dungeon automatisch erkennen"),
        new("boss", "Besiege den ersten Boss. Drück F8, wenn er tot ist.", "Boss-Phasen, Kampfende"),
        new("death", "Wenn es passt: einmal sterben und wiederbeleben lassen. Sonst „Überspringen“.", "Todes-Rückblick"),
        new("dungeon_done", "Schließe den Dungeon ab und verlasse ihn.", "Abschluss, Checkliste automatisch abhaken"),
        new("party_leave", "Verlasse die Gruppe.", "Gruppen-Ereignisse"),
    ];

    private readonly Lock gate = new();
    private StreamWriter? writer;
    private DateTime lastFlush;

    public ResearchRecorder(TcpStreamBuffer streamBuffer) =>
        streamBuffer.RawDataAdded += (key, payload, at) => Write(new RawChunk(at, key, payload));

    public bool IsRecording { get { lock (gate) return writer is not null; } }
    public int StepIndex { get; private set; }
    public string? FilePath { get; private set; }
    public DateTime StartedAt { get; private set; }
    public long Bytes { get; private set; }
    public ResearchStep? CurrentStep => IsRecording && StepIndex < Steps.Count ? Steps[StepIndex] : null;

    public event EventHandler? Changed;

    public string Start(string directory, string appVersion)
    {
        lock (gate)
        {
            if (writer is not null) return FilePath!;
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory, $"aufnahme_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            writer = new StreamWriter(FilePath, append: false, new UTF8Encoding(false));
            writer.WriteLine("# AetherDPS-Mitschnitt");
            writer.WriteLine("# Format: TIMESTAMP|STREAMKEY|HEX_DATA");
            writer.WriteLine($"# AetherDPS {appVersion}, geführte Aufnahme ab {DateTime.Now:O}");
            writer.WriteLine("# Enthält Namen von Spielern – nicht öffentlich teilen.");
            StartedAt = DateTime.Now;
            StepIndex = 0;
            Bytes = 0;
        }
        Mark("start", "begonnen", "");
        return FilePath!;
    }

    /// <summary>Aktuellen Schritt abschließen (erledigt oder übersprungen) und zum nächsten gehen.</summary>
    public void Next(bool done, string? text = null)
    {
        if (CurrentStep is not { } step) return;
        Mark(step.Key, done ? "erledigt" : "übersprungen", text ?? "");
        StepIndex++;
        if (StepIndex >= Steps.Count) Stop();
        else Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (gate)
        {
            if (writer is null) return;
            WriteMark("ende", "beendet", "");
            writer.Dispose();
            writer = null;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Mark(string key, string result, string text)
    {
        lock (gate) WriteMark(key, result, text);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void WriteMark(string key, string result, string text)
    {
        if (writer is null) return;
        var clean = text.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ');
        writer.WriteLine($"# MARK|{DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}|{key}|{result}|{clean}");
        writer.Flush();
    }

    private void Write(RawChunk chunk)
    {
        lock (gate)
        {
            if (writer is null) return;
            var time = DateTimeOffset.FromUnixTimeMilliseconds(chunk.ArrivedAtMs).UtcDateTime;
            writer.WriteLine($"{time.ToString("O", CultureInfo.InvariantCulture)}|{chunk.StreamKey}|{Convert.ToHexString(chunk.Payload)}");
            Bytes += chunk.Payload.Length;
            if (DateTime.Now - lastFlush > TimeSpan.FromSeconds(3))
            {
                writer.Flush();
                lastFlush = DateTime.Now;
            }
        }
    }

    /// <summary>Marken aus einem Mitschnitt lesen (für die Auswertung).</summary>
    public static IEnumerable<(DateTime At, string Key, string Result, string Text)> ReadMarks(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (!line.StartsWith("# MARK|", StringComparison.Ordinal)) continue;
            var parts = line.Split('|');
            if (parts.Length < 5) continue;
            if (!DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at))
                continue;
            yield return (at, parts[2], parts[3], parts[4]);
        }
    }
}
