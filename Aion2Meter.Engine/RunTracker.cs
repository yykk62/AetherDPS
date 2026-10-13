using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;

namespace Aion2Meter.Engine;

/// <summary>
/// Lauf-Modus (z. B. ein ganzer Dungeon): addiert ALLEN Schaden ab dem Start auf –
/// über alle Mobs und Kampfpausen hinweg, bis zum Stopp. Anders als Upstream,
/// das pro Ziel zählt und bei jedem neuen Mob umspringt.
/// Zusätzlich für Archiv und Kampfanalyse: Boss-Zeiten, Tode, Aktivität, Buff-Laufzeiten.
/// </summary>
public sealed class RunTracker
{
    /// <summary>Pausen bis zu dieser Länge zählen noch als Kampfzeit.</summary>
    public static readonly TimeSpan CombatGap = TimeSpan.FromSeconds(5);

    /// <summary>Pausen eines Spielers bis zu dieser Länge zählen noch als „aktiv“.</summary>
    public static readonly TimeSpan ActivityGap = TimeSpan.FromSeconds(2);

    private readonly EntityTracker entities;
    private readonly Lock gate = new();
    private readonly Dictionary<int, PlayerRun> players = [];
    private readonly Dictionary<int, TargetRun> targets = [];
    private readonly List<(DateTime Start, DateTime End)> segments = [];
    private DateTime? startedAt;
    private DateTime? stoppedAt;
    private DateTime? lastHit;
    private TimeSpan combatTime;

    /// <summary>Nach dem Stopp eines Laufs mit Schaden (für das Archiv).</summary>
    public event Action<RunSnapshot>? Completed;

    public RunTracker(CombatSessionManager sessions, EntityTracker entities)
    {
        this.entities = entities;
        sessions.DamageRecorded += Add;
        sessions.PlayerDied += OnPlayerDied;
        sessions.BuffRecorded += OnBuff;
        entities.EntityDied += OnEntityDied;
    }

    public bool IsRunning { get { lock (gate) return startedAt is not null && stoppedAt is null; } }

    /// <summary>
    /// Auto-Modus: Läuft gerade kein Lauf, startet dieser Treffer einen neuen, wenn die Funktion
    /// true liefert (z. B. Treffer auf einen Boss). Null = nur manuell per <see cref="Start"/>.
    /// </summary>
    public Func<PlayerDamage, bool>? AutoStart { get; set; }

    /// <summary>Letzter Treffer im laufenden Lauf.</summary>
    public DateTime? LastHit { get { lock (gate) return lastHit; } }

    public void Start(DateTime? now = null)
    {
        lock (gate) ResetLocked(now ?? DateTime.Now);
    }

    private void ResetLocked(DateTime start)
    {
        players.Clear();
        targets.Clear();
        segments.Clear();
        startedAt = start;
        stoppedAt = null;
        lastHit = null;
        combatTime = TimeSpan.Zero;
    }

    public void Stop(DateTime? now = null)
    {
        RunSnapshot? finished = null;
        lock (gate)
        {
            if (startedAt is null || stoppedAt is not null) return;
            stoppedAt = now ?? DateTime.Now;
            if (players.Count > 0) finished = SnapshotLocked(stoppedAt.Value);
        }
        if (finished is not null) Completed?.Invoke(finished);
    }

    private bool Recording => startedAt is not null && stoppedAt is null;

    private void Add(PlayerDamage hit)
    {
        lock (gate)
        {
            if (!Recording)
            {
                if (AutoStart?.Invoke(hit) != true) return;
                ResetLocked(hit.DateTime);
            }
            var at = hit.DateTime;

            if (lastHit is DateTime previous && at - previous is var gap && gap > TimeSpan.Zero && gap <= CombatGap)
            {
                combatTime += gap;
                segments[^1] = (segments[^1].Start, at);
            }
            else if (lastHit is null || at - lastHit > CombatGap)
            {
                segments.Add((at, at));
            }
            if (lastHit is null || at > lastHit) lastHit = at;

            int id = hit.SourceEntity.Id;
            if (!players.TryGetValue(id, out var player))
                players[id] = player = new PlayerRun(id);
            player.ClassId = (int)(hit.CharacterClass?.Id ?? player.ClassId);
            player.Add(hit);

            int targetId = hit.TargetEntity.Id;
            if (!targets.TryGetValue(targetId, out var target))
                targets[targetId] = target = new TargetRun(targetId, at);
            target.LastHit = at;
            target.Damage += hit.Damage;
        }
    }

    private void OnPlayerDied(int playerId)
    {
        lock (gate)
        {
            if (!Recording) return;
            if (!players.TryGetValue(playerId, out var player))
                players[playerId] = player = new PlayerRun(playerId);
            player.Deaths.Add(DateTime.Now);
        }
    }

    private void OnBuff(BuffEvent buff)
    {
        lock (gate)
        {
            if (!Recording || buff.DurationMs == 0) return;
            if (!players.TryGetValue(buff.EntityId, out var player))
                players[buff.EntityId] = player = new PlayerRun(buff.EntityId);
            if (!player.Buffs.TryGetValue(buff.BuffId, out var run))
                player.Buffs[buff.BuffId] = run = new BuffRun(string.IsNullOrWhiteSpace(buff.BuffName) ? $"Buff {buff.BuffId}" : buff.BuffName);
            run.Intervals.Add((buff.AppliedAt, buff.AppliedAt.AddMilliseconds(buff.DurationMs)));
        }
    }

    private void OnEntityDied(int entityId)
    {
        lock (gate)
        {
            if (Recording && targets.TryGetValue(entityId, out var target)) target.DiedAt ??= DateTime.Now;
        }
    }

    public RunSnapshot Snapshot(DateTime? now = null)
    {
        lock (gate) return SnapshotLocked(stoppedAt ?? now ?? DateTime.Now);
    }

    private RunSnapshot SnapshotLocked(DateTime end)
    {
        var elapsed = startedAt is DateTime s ? end - s : TimeSpan.Zero;
        // Mindestens 1 s, damit der erste Treffer keine absurde DPS liefert.
        double seconds = Math.Max(1, combatTime.TotalSeconds);
        var damaging = players.Values.Where(p => p.Hits > 0).ToList();
        long total = damaging.Sum(p => p.Damage);

        var rows = damaging.Select(p =>
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
                p.Crits,
                p.Deaths.Count,
                Math.Min(100, p.ActiveTime.TotalSeconds * 100 / seconds));
        }).OrderByDescending(r => r.Damage).ToList();

        var bosses = targets.Values
            .Select(t => (Target: t, Mob: entities.GetTargetMob(t.Id)))
            .Where(x => x.Mob is { MobCode: > 0 } mob && mob.IsBoss && !mob.IsDummy)
            .Select(x => new RunBoss(
                GermanGameData.MobName(x.Mob!),
                startedAt is DateTime st ? x.Target.FirstHit - st : TimeSpan.Zero,
                (x.Target.DiedAt ?? x.Target.LastHit) - x.Target.FirstHit,
                x.Target.DiedAt is not null,
                x.Target.Damage))
            .OrderBy(b => b.StartOffset)
            .ToList();

        return new RunSnapshot(startedAt is not null, stoppedAt is not null, startedAt ?? end, elapsed, combatTime,
            total, total / seconds, rows, bosses);
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

    /// <summary>Buff-Laufzeit eines Spielers in Prozent der Kampfzeit (nur Kampfphasen zählen).</summary>
    public IReadOnlyList<RunBuff> Buffs(int playerId)
    {
        lock (gate)
        {
            if (!players.TryGetValue(playerId, out var player) || segments.Count == 0) return [];
            double combat = Math.Max(1, segments.Sum(s => (s.End - s.Start).TotalSeconds));
            return player.Buffs.Values
                .Select(b => new RunBuff(b.Name, Math.Min(100, Overlap(b.Intervals, segments) * 100 / combat), b.Intervals.Count))
                .Where(b => b.UptimePercent > 0)
                .OrderByDescending(b => b.UptimePercent)
                .ToList();
        }
    }

    /// <summary>Todeszeitpunkte relativ zum Laufstart.</summary>
    public IReadOnlyList<TimeSpan> Deaths(int playerId)
    {
        lock (gate)
        {
            if (startedAt is not DateTime s || !players.TryGetValue(playerId, out var player)) return [];
            return player.Deaths.Select(d => d - s).ToList();
        }
    }

    /// <summary>Gesamtlänge der Überschneidung zweier Intervall-Listen in Sekunden.</summary>
    public static double Overlap(IEnumerable<(DateTime Start, DateTime End)> a, IReadOnlyList<(DateTime Start, DateTime End)> b)
    {
        // a vereinigen (überlappende Buff-Anwendungen nicht doppelt zählen)
        var merged = new List<(DateTime Start, DateTime End)>();
        foreach (var iv in a.OrderBy(x => x.Start))
        {
            if (merged.Count > 0 && iv.Start <= merged[^1].End)
                merged[^1] = (merged[^1].Start, iv.End > merged[^1].End ? iv.End : merged[^1].End);
            else
                merged.Add(iv);
        }

        double sum = 0;
        foreach (var x in merged)
            foreach (var y in b)
            {
                var start = x.Start > y.Start ? x.Start : y.Start;
                var end = x.End < y.End ? x.End : y.End;
                if (end > start) sum += (end - start).TotalSeconds;
            }
        return sum;
    }

    private sealed class PlayerRun(int id)
    {
        public int Id { get; } = id;
        public int ClassId { get; set; }
        public long Damage { get; private set; }
        public int Hits { get; private set; }
        public int Crits { get; private set; }
        public TimeSpan ActiveTime { get; private set; }
        public Dictionary<int, SkillRun> SkillsById { get; } = [];
        public Dictionary<int, BuffRun> Buffs { get; } = [];
        public List<DateTime> Deaths { get; } = [];
        private DateTime? lastHit;

        public void Add(PlayerDamage hit)
        {
            Damage += hit.Damage;
            Hits++;
            if (hit.IsCritical) Crits++;
            if (lastHit is DateTime last && hit.DateTime - last is var gap && gap > TimeSpan.Zero && gap <= ActivityGap)
                ActiveTime += gap;
            if (lastHit is null || hit.DateTime > lastHit) lastHit = hit.DateTime;

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

    private sealed class BuffRun(string name)
    {
        public string Name { get; } = name;
        public List<(DateTime Start, DateTime End)> Intervals { get; } = [];
    }

    private sealed class TargetRun(int id, DateTime firstHit)
    {
        public int Id { get; } = id;
        public DateTime FirstHit { get; } = firstHit;
        public DateTime LastHit { get; set; } = firstHit;
        public DateTime? DiedAt { get; set; }
        public long Damage { get; set; }
    }
}

public sealed record RunPlayer(int PlayerId, string Name, bool IsUser, bool IsIdentified, int ClassId, string ClassName,
    long Damage, double Dps, double Share, int Hits, int Crits, int Deaths = 0, double ActivityPercent = 0);

public sealed record RunSkill(string Name, long Damage, double Share, int Hits, double CritRate);

public sealed record RunBuff(string Name, double UptimePercent, int Applications);

public sealed record RunBoss(string Name, TimeSpan StartOffset, TimeSpan Duration, bool Killed, long Damage);

public sealed record RunSnapshot(bool Started, bool Stopped, DateTime StartedAt, TimeSpan Elapsed, TimeSpan CombatTime,
    long TotalDamage, double PartyDps, IReadOnlyList<RunPlayer> Players, IReadOnlyList<RunBoss> Bosses);
