using System.Globalization;
using System.Text;

namespace AetherDPS.Engine.Capture;

/// <summary>Ein Stück TCP-Nutzdaten, wie es vom Netzwerk kam (Server → Client).</summary>
public readonly record struct RawChunk(long ArrivedAtMs, string StreamKey, byte[] Payload);

/// <summary>
/// Mitschnitt-Format, kompatibel zu RATmeters PacketLogs:
/// <c>ZEITSTEMPEL(ISO-8601)|STREAMKEY|HEX</c>, Kommentarzeilen beginnen mit '#'.
/// </summary>
public static class CaptureFile
{
    public static void Write(string path, IEnumerable<RawChunk> chunks, IEnumerable<string>? headerLines = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        writer.WriteLine("# AetherDPS-Mitschnitt");
        writer.WriteLine("# Format: TIMESTAMP|STREAMKEY|HEX_DATA");
        foreach (var line in headerLines ?? [])
            writer.WriteLine($"# {line}");
        writer.WriteLine();
        foreach (var chunk in chunks)
        {
            var time = DateTimeOffset.FromUnixTimeMilliseconds(chunk.ArrivedAtMs).UtcDateTime;
            writer.WriteLine($"{time.ToString("O", CultureInfo.InvariantCulture)}|{chunk.StreamKey}|{Convert.ToHexString(chunk.Payload)}");
        }
    }

    public static IEnumerable<RawChunk> Read(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (TryParseLine(line, out var chunk))
                yield return chunk;
        }
    }

    public static bool TryParseLine(string line, out RawChunk chunk)
    {
        chunk = default;
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            return false;

        var parts = line.Split('|');
        if (parts.Length != 3)
            return false;

        if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var time))
            return false;

        try
        {
            var payload = Convert.FromHexString(parts[2]);
            var ms = new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            chunk = new RawChunk(ms, parts[1], payload);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
