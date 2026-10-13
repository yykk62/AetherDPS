using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;

namespace Aion2Meter.Engine;

/// <summary>
/// Lauf-Modus (z. B. ein ganzer Dungeon): addiert ALLEN Schaden ab dem Start auf –
/// über alle Mobs und Kampfpausen hinweg, bis zum Stopp. Anders als Upstream,
/// das pro Ziel zählt und bei jedem neuen Mob umspringt.
/// </summary>
public sealed class RunTracker
{
    /// <summary>Pausen bis zu dieser Länge zählen noch als Kampfzeit.</summary>
    public static readonly TimeSpan CombatGap = TimeSpan.FromSeconds(5);

    private readonly EntityTracker entities;
    private readonly Lock gate = new();
    private readonly Dictionary<int, PlayerRun> players = [];
    private DateTime? startedAt;
    private DateTime? stoppedAt;
    private DateTime? lastHit;
    private TimeSpan combatTime;

    public RunTracker(CombatSessionManager sessions, EntityTracker entities)
    {
        this.entities = entities;
        sessions.DamageRecorded += Add;
    }

    public bool IsRunning { get { lock (gate) return startedAt is not null && stoppedAt is null; } }

    public void Start(DateTime? now = null)
    {
        lock (gate)
        {
            players.Clear();
            startedAt = now ?? DateTime.Now;
            stoppedAt = null;
            lastHit = null;
            combatTime = TimeSpan.Zero;
        }
    }

    public void Stop(DateTime? now = null)
    {
        lock (gate)
        {
            if (startedAt is not null && stoppedAt is null) stoppedAt = now ?? DateTime.Now;
        }
    }

    private void Add(PlayerDamage hit)
    {
        lock (gate)
        {
            if (startedAt is null || stoppedAt is not null) return;

            if (lastHit is DateTime previous)
            {
                var gap = hit.DateTime - previous;
                if (gap > TimeSpan.Zero && gap <= CombatGap) combatTime += gap;
            }
            if (lastHit is null || hit.DateTime > lastHit) lastHit = hit.DateTime;

            int id = hit.SourceEntity.Id;
            if (!players.TryGetValue(id, out var player))
                players[id] = player = new PlayerRun(id);
            player.ClassId = (int)(hit.CharacterClass?.Id ?? player.ClassId);
            player.Add(hit);
        }
    }

    public RunSnapshot Snapshot(DateTime? now = null)
    {
        lock (gate)
        {
            var end = stoppedAt ?? now ?? DateTime.Now;
            var elapsed = startedAt is DateTime s ? end - s : TimeSpan.Zero;
            // Mindestens 1 s, damit der erste Treffer keine absurde DPS liefert.
            double seconds = Math.Max(1, combatTime.TotalSeconds);
            long total = players.Values.Sum(p => p.Damage);

            var rows = players.Values.Select(p =>
            {
                var entity = entities.GetPlayerEntity(p.Id);
                return new RunPlayer(
                    p.Id,
                    entity?.Name ?? $"Player_{p.Id}",
                    entity?.IsUser ?? false,
                    entity?.IsIdentified ?? false,
                    (int)(entity?.CharacterClass?.Id ?? p.ClassId),
                    entity?.CharacterClass?.Name ?? "",
                    p.Damage,
                    p.Damage / seconds,
                    total > 0 ? p.Damage * 100.0 / total : 0,
                    p.Hits,
                    p.Crits);
            }).OrderByDescending(r => r.Damage).ToList();

            return new RunSnapshot(startedAt is not null, stoppedAt is not null, elapsed, combatTime, total, total / seconds, rows);
        }
    }

    /// <param name="summonIds">Zugeschlagene Beschwörungen (siehe SummonMerger) – deren Skills erscheinen mit Präfix.</param>
    public IReadOnlyList<RunSkill> Skills(int playerId, IEnumerable<long>? summonIds = null)
    {
        lock (gate)
        {
            var parts = new List<(string Prefix, PlayerRun Run)>();
            if (players.TryGetValue(playerId, out var player)) parts.Add(("", player));
            foreach (var id in summonIds ?? [])
                if (players.TryGetValue((int)id, out var summon)) parts.Add(("Beschwörung · ", summon));
            if (parts.Count == 0) return [];

            var skills = parts
                .SelectMany(p => p.Run.SkillsById.Values.Select(s => (Name: p.Prefix + s.Name, s.Damage, s.Hits, s.Crits)))
                .GroupBy(s => s.Name)
                .Select(g => (Name: g.Key, Damage: g.Sum(s => s.Damage), Hits: g.Sum(s => s.Hits), Crits: g.Sum(s => s.Crits)))
                .ToList();
            long total = skills.Sum(s => s.Damage);
            return skills
                .Select(s => new RunSkill(s.Name, s.Damage, total > 0 ? s.Damage * 100.0 / total : 0, s.Hits,
                    s.Hits > 0 ? s.Crits * 100.0 / s.Hits : 0))
                .OrderByDescending(s => s.Damage)
                .ToList();
        }
    }

    private sealed class PlayerRun(int id)
    {
        public int Id { get; } = id;
        public int ClassId { get; set; }
        public long Damage { get; private set; }
        public int Hits { get; private set; }
        public int Crits { get; private set; }
        public Dictionary<int, SkillRun> SkillsById { get; } = [];

        public void Add(PlayerDamage hit)
        {
            Damage += hit.Damage;
            Hits++;
            if (hit.IsCritical) Crits++;

            int skillId = hit.Skill?.Id ?? 0;
            if (!SkillsById.TryGetValue(skillId, out var skill))
            {
                var name = hit.Skill?.Name;
                if (string.IsNullOrWhiteSpace(name)) name = $"Skill {skillId}";
                SkillsById[skillId] = skill = new SkillRun(name);
            }
            skill.Damage += hit.Damage;
            skill.Hits++;
            if (hit.IsCritical) skill.Crits++;
        }
    }

    private sealed class SkillRun(string name)
    {
        public string Name { get; } = name;
        public long Damage { get; set; }
        public int Hits { get; set; }
        public int Crits { get; set; }
    }
}

public sealed record RunPlayer(int PlayerId, string Name, bool IsUser, bool IsIdentified, int ClassId, string ClassName,
    long Damage, double Dps, double Share, int Hits, int Crits);

public sealed record RunSkill(string Name, long Damage, double Share, int Hits, double CritRate);

public sealed record RunSnapshot(bool Started, bool Stopped, TimeSpan Elapsed, TimeSpan CombatTime, long TotalDamage,
    double PartyDps, IReadOnlyList<RunPlayer> Players);
