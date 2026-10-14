using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;

namespace AetherDPS.Engine;

public sealed record DeathHit(string Source, string Skill, long Damage, int Count);

/// <summary>Woran bin ich gestorben? Schaden auf dich in den letzten Sekunden vor dem Tod.</summary>
public sealed record DeathReport(DateTime At, TimeSpan Window, long TotalDamage, IReadOnlyList<DeathHit> Hits);

/// <summary>
/// Merkt sich den Schaden, den DU in den letzten Sekunden bekommst (RATmeter verwirft Treffer von
/// Mobs, wir fangen sie über <c>RawDamage</c> ab), und meldet beim eigenen Tod eine Zusammenfassung.
/// </summary>
public sealed class DeathRecap
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(8);

    private readonly SelfIdentity self;
    private readonly EntityTracker entities;
    private readonly Lock gate = new();
    private readonly Queue<(DateTime At, int Actor, int Skill, long Damage)> incoming = new();
    private DateTime lastReport;

    public DeathRecap(CombatSessionManager sessions, EntityTracker entities, SelfIdentity self)
    {
        this.self = self;
        this.entities = entities;
        sessions.RawDamage += OnRawDamage;
        entities.EntityDied += OnDied;
    }

    public event Action<DeathReport>? Died;

    /// <summary>Uhr – für die Auswertung von Mitschnitten auf Paketzeit umstellbar.</summary>
    public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

    public int IncomingHits { get; private set; }

    private void OnRawDamage(int targetId, int actorId, int skillCode, long damage)
    {
        if (self.SelfId != targetId || actorId == targetId || damage <= 0) return;
        // Heilungen und Buffs von Mitspielern kommen im selben Paket – nur Gegner zählen (14.10.: Heiler stand sonst im Rückblick)
        if (AionDpsMeter.Core.GameData.Services.GameDataProvider.Instance.IsHealingSkill(skillCode)) return;
        if (entities.GetPlayerEntity(actorId) is not null) return;
        var now = Clock();
        lock (gate)
        {
            incoming.Enqueue((now, actorId, skillCode, damage));
            IncomingHits++;
            while (incoming.Count > 0 && now - incoming.Peek().At > Window) incoming.Dequeue();
        }
    }

    private void OnDied(int entityId)
    {
        if (self.SelfId != entityId) return;
        var now = Clock();
        List<(DateTime At, int Actor, int Skill, long Damage)> hits;
        lock (gate)
        {
            if (now - lastReport < TimeSpan.FromSeconds(3)) return;   // doppelte Todespakete
            lastReport = now;
            hits = incoming.Where(h => now - h.At <= Window).ToList();
            incoming.Clear();
        }
        var grouped = hits
            .GroupBy(h => (h.Actor, h.Skill))
            .Select(g => new DeathHit(SourceName(g.Key.Actor), SkillName(g.Key.Skill), g.Sum(h => h.Damage), g.Count()))
            .OrderByDescending(h => h.Damage)
            .ToList();
        Died?.Invoke(new DeathReport(now, Window, hits.Sum(h => h.Damage), grouped));
    }

    private string SourceName(int actorId)
    {
        if (entities.GetTargetMob(actorId) is { MobCode: > 0 } mob) return GermanGameData.MobName(mob);
        if (entities.GetPlayerEntity(actorId) is { } player && !player.Name.StartsWith("Player_", StringComparison.Ordinal)) return player.Name;
        return "Unbekannt";
    }

    private static string SkillName(int skillCode)
    {
        if (skillCode == 0) return "Angriff";
        var name = AionDpsMeter.Core.GameData.Services.GameDataProvider.Instance.GetSkillOrDefault(skillCode)?.Name;
        return string.IsNullOrWhiteSpace(name) || name.StartsWith("Unknown", StringComparison.Ordinal) ? "Angriff" : name;
    }
}
