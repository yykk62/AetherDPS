using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AetherDPS.Engine.Community;

/// <summary>
/// Patch-Tag-Schutz: Nach einem Spiel-Update verschieben sich oft die Paketnummern (Opcodes), dann
/// versteht der Meter nichts mehr und bleibt leer. Kommen viele Pakete an, aber keins der
/// Grundpakete (Zeit, Spawns, Leben, Schaden, Kartenwechsel), ist das sehr wahrscheinlich so.
/// </summary>
public sealed class PatchDetector
{
    /// <summary>So viele Pakete ohne ein einziges Grundpaket gelten als „versteht nichts mehr“.</summary>
    public const int Threshold = 3000;

    private static readonly HashSet<ushort> Core =
    [
        PacketOpcodes.ServerTime, PacketOpcodes.Damage, PacketOpcodes.DotDamage, PacketOpcodes.RemainHp,
        PacketOpcodes.MobSummon, PacketOpcodes.EntityDeath, PacketOpcodes.PlayerInfo, ZoneTracker.OpMapLoad,
    ];

    private int sinceCore;
    private volatile bool broken;

    public PatchDetector(PacketDispatchService dispatch)
    {
        var previous = dispatch.Observer;
        dispatch.Observer = (opcode, data) =>
        {
            previous?.Invoke(opcode, data);
            Observe(opcode);
        };
    }

    /// <summary>Der Meter versteht die Spieldaten vermutlich nicht mehr (Spiel-Update).</summary>
    public bool LikelyBroken => broken;

    /// <summary>true = gerade kaputt erkannt, false = läuft wieder.</summary>
    public event Action<bool>? Changed;

    private void Observe(ushort opcode)
    {
        if (opcode == PacketOpcodes.CompressedStream) return;   // nur Hülle, der Inhalt kommt einzeln
        if (Core.Contains(opcode))
        {
            sinceCore = 0;
            if (broken)
            {
                broken = false;
                Changed?.Invoke(false);
            }
            return;
        }
        if (++sinceCore == Threshold && !broken)
        {
            broken = true;
            Changed?.Invoke(true);
        }
    }
}
