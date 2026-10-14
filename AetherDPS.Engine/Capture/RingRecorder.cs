using AionDpsMeter.Services.PacketCapture;

namespace AetherDPS.Engine.Capture;

/// <summary>
/// Hält die letzten Minuten Rohdaten im Speicher. "Problem melden" schreibt sie
/// als Mitschnitt auf die Platte – so braucht es nie eine extra Test-Session.
/// </summary>
public sealed class RingRecorder
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(5);
    private const long MaxBytes = 64L * 1024 * 1024;

    private readonly Queue<RawChunk> chunks = new();
    private readonly Lock gate = new();
    private readonly TimeSpan window;
    private long bytes;

    public RingRecorder(TcpStreamBuffer streamBuffer) : this(streamBuffer, DefaultWindow) { }

    public RingRecorder(TcpStreamBuffer streamBuffer, TimeSpan window)
    {
        this.window = window;
        streamBuffer.RawDataAdded += (key, payload, at) => Add(new RawChunk(at, key, payload));
    }

    public int Count { get { lock (gate) return chunks.Count; } }

    public void Add(RawChunk chunk)
    {
        lock (gate)
        {
            chunks.Enqueue(chunk);
            bytes += chunk.Payload.Length;
            long cutoff = chunk.ArrivedAtMs - (long)window.TotalMilliseconds;
            while (chunks.Count > 0 && (chunks.Peek().ArrivedAtMs < cutoff || bytes > MaxBytes))
                bytes -= chunks.Dequeue().Payload.Length;
        }
    }

    /// <summary>Schreibt den aktuellen Puffer und liefert den Dateipfad (null, wenn leer).</summary>
    public string? SaveSnapshot(string directory, string reason, string appVersion)
    {
        RawChunk[] snapshot;
        lock (gate) snapshot = chunks.ToArray();
        if (snapshot.Length == 0) return null;

        var path = Path.Combine(directory, $"problem_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        CaptureFile.Write(path, snapshot,
        [
            $"AetherDPS {appVersion}, gespeichert {DateTime.Now:O}",
            $"Grund: {reason}",
            "Enthält Namen deiner Gruppe – nicht öffentlich teilen.",
        ]);
        return path;
    }
}
