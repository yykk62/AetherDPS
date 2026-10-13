using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Services.Services.Session;

namespace Aion2Meter.Engine;

/// <summary>
/// Bereitschaft vs. aktiv. In Bereitschaft wird KEIN Kampf erfasst (Schaden,
/// DoTs, Buffs, Tode, Abklingzeiten werden verworfen). Durch kommen nur
/// Identitätsdaten – Namen, Gruppe, Mobs, Ping –, damit beim Aktivieren dein
/// eigener Name schon bekannt ist (das Spiel schickt ihn nur beim Login/Teleport).
/// </summary>
public sealed class TrackingMode
{
    private static readonly HashSet<ushort> AllowedInStandby =
    [
        PacketOpcodes.PlayerInfo,
        PacketOpcodes.OtherPlayersInfo,
        PacketOpcodes.GlobalSessIdLinking,
        PacketOpcodes.PartyInfo,
        PacketOpcodes.PlayerStats,
        PacketOpcodes.MobSummon,
        PacketOpcodes.RemainHp,
        PacketOpcodes.ServerTime,
    ];

    private readonly CombatSessionManager sessions;
    private readonly RunTracker run;
    private volatile bool active;

    public TrackingMode(PacketDispatchService dispatch, CombatSessionManager sessions, RunTracker run)
    {
        this.sessions = sessions;
        this.run = run;
        dispatch.Gate = opcode => active || AllowedInStandby.Contains(opcode);
    }

    public bool IsActive => active;

    public event EventHandler? Changed;

    /// <summary>Aktiv schalten – die Statistik beginnt frisch ab jetzt.</summary>
    public void Activate()
    {
        if (active) return;
        sessions.Reset();
        run.Start();
        active = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>In Bereitschaft – laufende Kämpfe werden noch in die Historie geschrieben.</summary>
    public void Standby()
    {
        if (!active) return;
        active = false;
        run.Stop();
        sessions.CompleteAndPersistActiveSessions();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle()
    {
        if (active) Standby();
        else Activate();
    }
}
