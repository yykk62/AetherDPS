using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;

namespace AetherDPS.Engine.Community;

public enum PresenceKind { WaitingForGame, Standby, OpenWorld, Dungeon, BossFight }

/// <summary>Was der Spieler gerade tut – ohne Namen von Spielern oder Charakteren.</summary>
public sealed record PresenceInfo(PresenceKind Kind, DateTime Since, string? Dungeon = null, string? Boss = null);

/// <summary>Fertiger Text für Discord (Rich Presence).</summary>
public sealed record PresenceActivity(string Details, string? State, DateTime Since, string LargeText,
    string DownloadLabel, string DiscordLabel);

/// <summary>
/// Leitet aus Dungeon-Erkennung, Bosskämpfen und Modus ab, was im Discord-Profil steht.
/// Bosskampf = du oder deine Gruppe habt in den letzten <see cref="BossTimeout"/> einen Boss getroffen
/// (geht nur, solange erfasst wird – in Bereitschaft verwirft der Meter Schaden).
/// </summary>
public sealed class PresenceTracker
{
    public static readonly TimeSpan BossTimeout = TimeSpan.FromSeconds(30);

    private readonly ZoneTracker zones;
    private readonly TrackingMode mode;
    private readonly Lock gate = new();
    private readonly DateTime startedAt = DateTime.Now;
    private int bossId;
    private string? bossName;
    private DateTime bossSince, bossLastHit;

    /// <summary>Läuft AION 2? (von der App gesetzt; null = nicht prüfen, z. B. bei Wiedergaben)</summary>
    public Func<bool>? GameRunning { get; set; }

    public PresenceTracker(ZoneTracker zones, TrackingMode mode, CombatSessionManager sessions, EntityTracker entities, OwnSide own)
    {
        this.zones = zones;
        this.mode = mode;
        sessions.DamageRecorded += hit =>
        {
            if (!own.IsOwn(hit.SourceEntity.Id)) return;
            if (entities.GetTargetMob(hit.TargetEntity.Id) is not { MobCode: > 0 } mob || !mob.IsBoss) return;
            // Uhr des PCs statt Paketzeit – so stimmt es auch bei Wiedergaben
            var now = DateTime.Now;
            lock (gate)
            {
                if (bossId != mob.Id || now - bossLastHit > BossTimeout)
                {
                    bossId = mob.Id;
                    bossName = GameText.MobName(mob);
                    bossSince = now;
                }
                bossLastHit = now;
            }
        };
        entities.EntityDied += id =>
        {
            lock (gate)
                if (id == bossId) bossId = 0;
        };
    }

    public PresenceInfo Current(DateTime now)
    {
        lock (gate)
        {
            if (bossId != 0 && now - bossLastHit <= BossTimeout)
                return new(PresenceKind.BossFight, bossSince, zones.InInstance ? zones.DungeonName ?? "" : null, bossName);
        }
        if (zones.InInstance) return new(PresenceKind.Dungeon, zones.EnteredAt ?? startedAt, zones.DungeonName);
        if (GameRunning is { } running && !running()) return new(PresenceKind.WaitingForGame, startedAt);
        return new(mode.IsActive ? PresenceKind.OpenWorld : PresenceKind.Standby, startedAt);
    }
}

/// <summary>Baut die Zeilen fürs Discord-Profil. Spieler- und Charakternamen kommen hier nie hinein.</summary>
public static class PresenceText
{
    public const string DownloadUrl = "https://github.com/yykk62/AetherDPS/releases/latest";
    public const string DiscordUrl = "https://discord.gg/xExgsyDEP2";

    /// <summary>Discord: Texte 2–128 Zeichen, Knopf-Beschriftung höchstens 32.</summary>
    public const int MaxText = 128, MaxButton = 32;

    public static PresenceActivity Build(PresenceInfo info, bool showNames)
    {
        string? dungeon = showNames && !string.IsNullOrWhiteSpace(info.Dungeon) ? info.Dungeon : null;
        string? boss = showNames && !string.IsNullOrWhiteSpace(info.Boss) ? info.Boss : null;
        string details = info.Kind switch
        {
            PresenceKind.BossFight => boss is null ? Loc.T("presence.boss_hidden") : Loc.T("presence.boss", boss),
            PresenceKind.Dungeon => dungeon is null ? Loc.T("presence.dungeon_hidden") : Loc.T("presence.dungeon", dungeon),
            PresenceKind.OpenWorld => Loc.T("presence.open_world"),
            PresenceKind.WaitingForGame => Loc.T("presence.waiting"),
            _ => Loc.T("presence.standby"),
        };
        // Zweite Zeile: beim Boss im Dungeon der Dungeon, sonst ein Satz über den Meter
        string state = info is { Kind: PresenceKind.BossFight, Dungeon: not null }
            ? dungeon is null ? Loc.T("presence.dungeon_hidden") : Loc.T("presence.dungeon", dungeon)
            : Loc.T("presence.tagline");
        return new PresenceActivity(Clip(Loc.T("presence.game", details), MaxText), Clip(state, MaxText), info.Since,
            Clip(Loc.T("presence.large_text"), MaxText),
            Clip(Loc.T("presence.button_download"), MaxButton), Clip(Loc.T("presence.button_discord"), MaxButton));
    }

    public static string Clip(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max) return text.Length >= 2 ? text : text.PadRight(2);
        return text[..(max - 1)].TrimEnd() + "…";
    }
}
