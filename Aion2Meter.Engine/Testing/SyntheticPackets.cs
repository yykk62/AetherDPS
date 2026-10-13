using K4os.Compression.LZ4;

namespace Aion2Meter.Engine.Testing;

/// <summary>
/// Baut Pakete im AION-2-Format nach (Stand Protokoll Okt. 2026, siehe docs/PROTOKOLL.md).
/// Für Tests und Demo-Mitschnitte – ändert sich das Protokoll, schlagen die Tests an.
/// </summary>
public static class SyntheticPackets
{
    public const ushort OpDamage = 0x3804;

    /// <summary>Keepalive, an dem sich der Stream synchronisiert (0E 00 36 …, 11 Byte).</summary>
    public static byte[] Heartbeat() => [0x0E, 0x00, 0x36, 0, 0, 0, 0, 0, 0, 0, 0];

    public static byte[] Damage(int targetId, int actorId, int skillCode, long damage, bool critical = false)
    {
        var body = new List<byte>();
        AddU16(body, OpDamage);
        AddVarInt(body, (uint)targetId);
        AddVarInt(body, 4);            // switch: 4 = ohne Richtungs-/Flag-Block
        AddVarInt(body, 0);            // unbekanntes Flag
        AddVarInt(body, (uint)actorId);
        AddU32(body, (uint)skillCode);
        body.Add(0);
        AddVarInt(body, critical ? 3u : 1u);
        AddU32(body, 0);
        body.AddRange(new byte[4]);
        AddVarInt(body, 0);
        AddVarInt(body, (uint)damage);
        return Frame(body);
    }

    /// <summary>LZ4-Container (Opcode FF FF) mit mehreren inneren Paketen.</summary>
    public static byte[] Compressed(params byte[][] inner)
    {
        var plain = inner.SelectMany(p => p).ToArray();
        var target = new byte[LZ4Codec.MaximumOutputSize(plain.Length)];
        int written = LZ4Codec.Encode(plain, target);

        var body = new List<byte> { 0xFF, 0xFF };
        AddU32(body, (uint)plain.Length);
        body.AddRange(target.AsSpan(0, written).ToArray());
        return Frame(body);
    }

    /// <summary>Längenpräfix: varint(L) + Body, mit L = Body-Länge + 4.</summary>
    private static byte[] Frame(List<byte> body)
    {
        var packet = new List<byte>();
        AddVarInt(packet, (uint)(body.Count + 4));
        packet.AddRange(body);
        return packet.ToArray();
    }

    private static void AddU16(List<byte> b, ushort v) { b.Add((byte)v); b.Add((byte)(v >> 8)); }

    private static void AddU32(List<byte> b, uint v)
    {
        for (int i = 0; i < 4; i++) b.Add((byte)(v >> (8 * i)));
    }

    private static void AddVarInt(List<byte> b, uint v)
    {
        while (v >= 0x80)
        {
            b.Add((byte)(v | 0x80));
            v >>= 7;
        }
        b.Add((byte)v);
    }
}
