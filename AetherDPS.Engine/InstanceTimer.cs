using System.Buffers.Binary;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AetherDPS.Engine;

/// <summary>
/// Zeitlimit im Dungeon: Der Server schickt Start und Ende (0x6100, Bytes „00 61“):
/// <c>Karte u32 | Art u8 | Start i64 ms | Ende i64 ms</c>. Gesehen in Draupnir (14.10.2026): Art 1 =
/// 30 s Start-Countdown, Art 2 = 60 Minuten für den ganzen Dungeon. Andere Arten (z. B. Boss-Enrage)
/// laufen genauso. Idee aus cyberbadger6969/aion2-dps-meter (GPL-3.0).
/// </summary>
public sealed class InstanceTimer
{
    public const ushort Op = 0x6100;

    private readonly Lock gate = new();
    private readonly Dictionary<int, (DateTime Start, DateTime End)> timers = [];

    public InstanceTimer(PacketDispatchService dispatch, ZoneTracker zones)
    {
        var previous = dispatch.Observer;
        dispatch.Observer = (opcode, data) =>
        {
            previous?.Invoke(opcode, data);
            if (opcode == Op && Parse(data) is { } t) Set(t.Kind, t.Start, t.End);
        };
        zones.Changed += entered =>
        {
            if (!entered) lock (gate) timers.Clear();
        };
    }

    /// <summary>Der Timer, der als Nächstes abläuft (Ende in der Zukunft), sonst null.</summary>
    public (int Kind, DateTime End, TimeSpan Total)? Current(DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        lock (gate)
        {
            var next = timers.Where(t => t.Value.End > now).OrderBy(t => t.Value.End).FirstOrDefault();
            return next.Value.End > now ? (next.Key, next.Value.End, next.Value.End - next.Value.Start) : null;
        }
    }

    private void Set(int kind, DateTime start, DateTime end)
    {
        lock (gate) timers[kind] = (start, end);
    }

    public static (int Map, int Kind, DateTime Start, DateTime End)? Parse(byte[] data)
    {
        int pos = 0;
        while (pos < data.Length && (data[pos] & 0x80) != 0) pos++;
        pos += 1 + 2;
        if (data.Length < pos + 21) return null;
        var b = data.AsSpan(pos);
        int map = BinaryPrimitives.ReadInt32LittleEndian(b);
        int kind = b[4];
        long start = BinaryPrimitives.ReadInt64LittleEndian(b[5..]);
        long end = BinaryPrimitives.ReadInt64LittleEndian(b[13..]);
        if (start < 1_600_000_000_000 || end <= start || end - start > TimeSpan.FromHours(24).TotalMilliseconds) return null;
        return (map, kind, DateTimeOffset.FromUnixTimeMilliseconds(start).LocalDateTime, DateTimeOffset.FromUnixTimeMilliseconds(end).LocalDateTime);
    }
}
