using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;

namespace AetherDPS.Engine;

/// <summary>
/// Bereitschaft vs. aktiv. In Bereitschaft wird KEIN Kampf erfasst (Schaden,
/// DoTs, Buffs, Tode, Abklingzeiten werden verworfen). Durch kommen nur
/// Identitätsdaten – Namen, Gruppe, Mobs, Ping –, damit beim Aktivieren dein
/// eigener Name schon bekannt ist (das Spiel schickt ihn nur beim Login/Teleport).
///
/// Auto-Modus (Einstellung): Es wird immer mitgelesen, ein Lauf startet beim ersten Treffer
/// auf einen Boss und landet nach <see cref="AutoRunEnd"/> ohne Kampf im Archiv. Kein Hotkey nötig.
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
        // für Buff-Leiste und Boss-Timer (keine Kampfaufzeichnung – der Lauf zählt nur im aktiven Modus)
        PacketOpcodes.EntityDeath,
        PacketOpcodes.BuffEffectA,
        PacketOpcodes.BuffEffectB,
        PacketOpcodes.SkillCd,
        ZoneTracker.OpServerSwitch,
        ZoneTracker.OpMapLoad,
    ];

    /// <summary>Auto-Modus: so lange ohne Kampf, dann gilt der Lauf (Dungeon) als beendet.</summary>
    public static readonly TimeSpan AutoRunEnd = TimeSpan.FromMinutes(5);

    private readonly CombatSessionManager sessions;
    private readonly RunTracker run;
    private readonly EntityTracker entities;
    private readonly OwnSide own;
    private volatile bool active;
    private volatile bool auto;

    public TrackingMode(PacketDispatchService dispatch, CombatSessionManager sessions, RunTracker run,
        EntityTracker entities, MeterSettings settings, OwnSide own, ZoneTracker zones)
    {
        // Auto-Modus: Dungeon betreten = neuer Lauf, verlassen = Lauf ins Archiv
        zones.Changed += entered =>
        {
            if (!auto) return;
            if (entered) run.Start();
            else run.Stop();
        };
        run.DungeonSource = () => zones.DungeonName;
        this.sessions = sessions;
        this.run = run;
        this.entities = entities;
        this.own = own;
        dispatch.Gate = opcode => active || auto || AllowedInStandby.Contains(opcode);
        SetAutoMode(settings.AutoTrack);
        settings.Changed += (_, _) => SetAutoMode(settings.AutoTrack);
    }

    /// <summary>Manuell per Hotkey aktiv geschaltet.</summary>
    public bool IsActive => active;

    /// <summary>Automatisch erfassen (Einstellung).</summary>
    public bool AutoMode => auto;

    /// <summary>Es wird gerade Kampf aufgezeichnet bzw. darauf gewartet (aktiv oder Auto-Modus).</summary>
    public bool IsCapturing => active || auto;

    public event EventHandler? Changed;

    public void SetAutoMode(bool on)
    {
        if (auto == on) return;
        if (on && active)
        {
            active = false;
            sessions.CompleteAndPersistActiveSessions();
        }
        run.Stop();
        auto = on;
        run.AutoStart = on ? IsBossHit : null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Du oder deine Gruppe treffen einen Boss – Fremde, die nebenan einen Feldboss schlagen, zählen nicht.</summary>
    private bool IsBossHit(PlayerDamage hit) =>
        own.IsOwn(hit.SourceEntity.Id)
        && entities.GetTargetMob(hit.TargetEntity.Id) is { MobCode: > 0 } mob && mob.IsBoss;

    /// <summary>Auto-Modus: Lauf nach längerer Kampfpause abschließen (→ Archiv). Einmal pro Sekunde aufrufen.</summary>
    public void Tick(DateTime now)
    {
        if (!auto || !run.IsRunning) return;
        // Uhr des PCs statt Paketzeit, damit es auch bei Wiedergaben alter Mitschnitte stimmt
        var last = run.LastHit;
        if (last != lastHit)
        {
            lastHit = last;
            lastHitSeenAt = now;
        }
        if (now - lastHitSeenAt > AutoRunEnd) run.Stop(now);
    }

    private DateTime? lastHit;
    private DateTime lastHitSeenAt;

    /// <summary>Aktiv schalten – die Statistik beginnt frisch ab jetzt.</summary>
    public void Activate()
    {
        if (active || auto) return;
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
