using K4os.Compression.LZ4;

namespace AetherDPS.Engine.Testing;

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

    public const ushort OpMobInfo = 0x3641, OpRemainHp = 0x8D00;

    /// <summary>
    /// Ein Mob erscheint (Code aus mobs.json, z. B. ein Boss) mit maximalem Leben.
    /// Aufbau wie MobPacketProcessor ihn liest: Mob-Code (3 Byte) · 00 00 02 · 01 · varint(HP).
    /// </summary>
    public static byte[] MobInfo(int mobId, int mobCode, long maxHp)
    {
        var body = new List<byte>();
        AddU16(body, OpMobInfo);
        AddVarInt(body, (uint)mobId);
        // Mob-Code muss bei Index 6 des Pakets liegen (der Parser rechnet ab dort)
        while (body.Count < 5) body.Add(0xFE);
        body.Add((byte)mobCode); body.Add((byte)(mobCode >> 8)); body.Add((byte)(mobCode >> 16));
        body.AddRange([0x00, 0x00, 0x02, 0x01]);
        AddVarInt64(body, (ulong)maxHp);
        body.AddRange(new byte[8]);
        return Frame(body);
    }

    /// <summary>Aktuelles Leben eines Mobs.</summary>
    public static byte[] RemainHp(int mobId, long hp)
    {
        var body = new List<byte>();
        AddU16(body, OpRemainHp);
        AddVarInt(body, (uint)mobId);
        AddVarInt(body, 0);
        AddVarInt(body, 0);
        AddVarInt(body, 0);
        for (int i = 0; i < 8; i++) body.Add((byte)((ulong)hp >> (8 * i)));
        return Frame(body);
    }

    /// <summary>Stagger-Leiste eines Bosses (0xE005): Zustand 3 = angekündigt, 2 = läuft, 0 = vorbei.</summary>
    public static byte[] Stagger(int bossId, int max, int current, byte state)
    {
        var body = new List<byte>();
        AddU16(body, 0xE005);
        AddVarInt(body, (uint)bossId);
        body.AddRange([0x03, 0x01]);
        AddU32(body, (uint)max);
        AddU32(body, (uint)current);
        body.Add(state);
        return Frame(body);
    }

    /// <summary>Stagger-Leiste gebrochen (geschafft).</summary>
    public static byte[] StaggerBroken(int bossId)
    {
        var body = new List<byte>();
        AddU16(body, 0xE005);
        AddVarInt(body, (uint)bossId);
        body.AddRange([0x00, 0x03, 0x02, 0x00]);
        return Frame(body);
    }

    /// <summary>Serverwechsel (0x3616) – die Adresse steht als Text im Paket.</summary>
    public static byte[] ServerSwitch(string address)
    {
        var body = new List<byte>();
        AddU16(body, 0x3616);
        body.AddRange([0xDC, 0x10]);
        body.Add((byte)address.Length);
        body.AddRange(System.Text.Encoding.ASCII.GetBytes(address));
        body.AddRange([0x10, 0x34, 0x62, 0x3B, 0x9F, 0x97, 0xAB, 0x0C]);
        return Frame(body);
    }

    /// <summary>Karte laden (0x3621): Zähler, Karten-Id, danach Koordinaten u. Ä.</summary>
    public static byte[] MapLoad(int mapId, uint counter = 1)
    {
        var body = new List<byte>();
        AddU16(body, 0x3621);
        AddU32(body, counter);
        AddU32(body, (uint)mapId);
        body.AddRange(new byte[44]);
        return Frame(body);
    }

    /// <summary>Feldboss-Liste (0x9101): lebende Bosse mit Position, Zeit = seit wann bzw. Respawn (Unix ms).</summary>
    public static byte[] FieldBossList(int map, params (int Slot, bool Alive, long TimeMs)[] entries)
    {
        var body = new List<byte>();
        AddU16(body, 0x9101);
        AddU16(body, 0);
        AddU32(body, (uint)map);
        body.Add((byte)entries.Length);
        foreach (var (slot, alive, time) in entries)
        {
            body.Add(alive ? (byte)1 : (byte)0);
            AddVarInt(body, (uint)slot);
            if (alive) body.AddRange(new byte[12]);
            body.AddRange(BitConverter.GetBytes(time));
        }
        body.AddRange(new byte[3]);
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

    private static void AddVarInt64(List<byte> b, ulong v)
    {
        while (v >= 0x80)
        {
            b.Add((byte)(v | 0x80));
            v >>= 7;
        }
        b.Add((byte)v);
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
