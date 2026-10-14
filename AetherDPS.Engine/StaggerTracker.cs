using AionDpsMeter.Core.GameData.Services;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;

namespace AetherDPS.Engine;

public sealed record StaggerSkill(string Skill, int Points, int Count);

public sealed record StaggerShare(string Name, bool IsUser, int Points, IReadOnlyList<StaggerSkill> Skills);

/// <summary>Eine Stagger-Phase eines Bosses: Leiste von <see cref="Max"/> auf 0 bringen, bevor die Zeit abläuft.</summary>
public sealed record StaggerPhase(int BossId, string BossName, int Max, int Remaining, bool Success,
    DateTime Start, DateTime End, IReadOnlyList<StaggerShare> Shares)
{
    public TimeSpan Duration => End - Start;
}

/// <summary>
/// Stagger-Leiste der Bosse (Opcode 0xE005, gefunden in der geführten Aufnahme vom 14.10.2026):
/// <c>[Boss] 03 01 [Max u32] [Aktuell u32] [Zustand]</c>, Zustand 3 = Phase angekündigt, 2 = läuft,
/// 0/1 = vorbei. <c>[Boss] 00 03 …</c> = Leiste gebrochen (geschafft). Jeder Skill senkt die Leiste
/// um einen festen Wert – daraus wird berechnet, wer wie viel beigetragen hat.
/// </summary>
public sealed class StaggerTracker
{
    public const ushort OpStagger = 0xE005;

    /// <summary>Stagger-Punkte pro Skill-Code aus der Aufnahme vom 14.10. – neue Werte lernt der Meter selbst.</summary>
    private static readonly Dictionary<int, int> KnownPoints = new()
    {
        [13050020] = 7, [13060020] = 7, [13070240] = 10, [13100020] = 2, [13110020] = 2, [13120020] = 2,
        [13130030] = 7, [13220001] = 10, [13270010] = 50, [13300020] = 20, [13350340] = 5, [13350341] = 5,
        [13360020] = 5, [14010243] = 16, [14050340] = 5, [14070000] = 10, [14080340] = 15, [14090010] = 7,
        [14110340] = 10, [14130030] = 10, [14170011] = 10, [14340230] = 1, [14360010] = 20, [17040000] = 2,
        [17050000] = 2, [17060003] = 35, [17070000] = 10, [17080000] = 10, [17350000] = 7, [17400020] = 20,
        [18040020] = 2, [18040230] = 2, [18050020] = 2, [18050230] = 2, [18060140] = 10, [18080001] = 10,
        [18090000] = 10, [18090340] = 10, [18100230] = 5, [18130020] = 20, [18150120] = 10, [18150240] = 10,
        [18210030] = 10, [18210340] = 10, [18220010] = 50, [18290030] = 15, [18290140] = 15,
    };

    private static readonly TimeSpan Match = TimeSpan.FromMilliseconds(60);   // Leiste und Treffer kommen im selben Netzwerkpaket

    private readonly EntityTracker entities;
    private readonly SelfIdentity self;
    private readonly Lock gate = new();
    private readonly Dictionary<int, int> points = new(KnownPoints);
    private readonly List<(DateTime At, int Target, int Actor, int Skill)> hits = [];
    private readonly List<(DateTime At, int Boss, int Amount)> drops = [];
    private Running? phase;
    private DateTime lastAnnounced = DateTime.MinValue;
    private (int Boss, DateTime At) lastEnded;

    private sealed class Running(int boss, int max, DateTime start)
    {
        public int Boss = boss, Max = max, Current = max;
        public DateTime Start = start;
        public bool Broken;
        public readonly Dictionary<int, Dictionary<int, (int Points, int Count)>> Shares = [];
    }

    public StaggerTracker(PacketDispatchService dispatch, CombatSessionManager sessions, EntityTracker entities, SelfIdentity self)
    {
        this.entities = entities;
        this.self = self;
        var previous = dispatch.Observer;
        dispatch.Observer = (opcode, data) =>
        {
            previous?.Invoke(opcode, data);
            if (opcode == OpStagger) OnPacket(data);
        };
        // Boss tot ohne Bruch der Leiste: Phase still verwerfen
        entities.EntityDied += id =>
        {
            lock (gate) if (phase?.Boss == id) phase = null;
        };
        sessions.RawDamage += (target, actor, skill, _) =>
        {
            lock (gate) hits.Add((Clock(), target, actor, skill));
        };
    }

    /// <summary>Uhr – für die Auswertung von Mitschnitten auf Paketzeit umstellbar.</summary>
    public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

    /// <summary>
    /// Aktuelle Phase (Boss, Leiste aktuell/max, Start, erwartetes Ende), null = keine.
    /// Das Ende ist geschätzt: Dauer der letzten verfehlten Phase dieses Bosses, sonst 18 s (Aufnahme 14.10.).
    /// </summary>
    public (string Boss, int Current, int Max, DateTime Start, DateTime ExpectedEnd)? Current
    {
        get
        {
            lock (gate)
                return phase is { } p
                    ? (BossName(p.Boss), p.Current, p.Max, p.Start, p.Start + durations.GetValueOrDefault(p.Boss, DefaultDuration))
                    : null;
        }
    }

    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(18);
    private readonly Dictionary<int, TimeSpan> durations = [];

    /// <summary>Phase angekündigt (einige Sekunden vorher) – Zeit, die großen Stagger-Skills aufzuheben.</summary>
    public event Action<string>? Announced;

    public event Action<StaggerPhase>? Finished;

    /// <summary>Deine Skills mit den meisten Stagger-Punkten (für die Ankündigung).</summary>
    public IReadOnlyList<(string Skill, int Points)> BestOwnSkills(int count = 3)
    {
        int classId = self.SelfId is int id && entities.GetPlayerEntity(id)?.CharacterClass?.Id is { } c ? (int)c : 0;
        if (classId == 0) return [];
        lock (gate)
            return points.Where(p => p.Key / 1_000_000 == classId && p.Value >= 10)
                .OrderByDescending(p => p.Value)
                .Select(p => (SkillName(p.Key), p.Value))
                .Where(p => p.Item1 != "?")
                .DistinctBy(p => p.Item1)
                .Take(count).ToList();
    }

    private void OnPacket(byte[] data)
    {
        if (!TryParse(data, out var boss, out var kind, out var max, out var current, out var state)) return;
        var now = Clock();
        string? announce = null;
        StaggerPhase? finished = null;
        lock (gate)
        {
            if (kind == 0)
            {
                // Leiste gebrochen = geschafft (bei Bossen mit Dauer-Leiste füllt sie sich danach neu)
                if (phase?.Boss == boss)
                {
                    phase.Broken = true;
                    Resolve(now, force: true);
                    finished = Complete(phase, now);
                    phase = null;
                }
            }
            else if (kind == 3) HandleGauge(boss, max, current, state, now, ref announce, ref finished);
        }
        if (announce is not null) Announced?.Invoke(announce);
        if (finished is not null) Finished?.Invoke(finished);
    }

    private void HandleGauge(int boss, int max, int current, int state, DateTime now, ref string? announce, ref StaggerPhase? finished)
    {
        {
            // Ankündigung: Zustand 3 oder – falls der fehlt (Phase 2 am 14.10.) – die volle Leiste beim Start
            // (nicht direkt nach einem Bruch – manche Bosse füllen die Leiste sofort wieder auf)
            if ((state == 3 || (state == 2 && phase is null && current >= max)) && now - lastAnnounced > TimeSpan.FromSeconds(30)
                && !(lastEnded.Boss == boss && now - lastEnded.At < TimeSpan.FromSeconds(30)))
            {
                announce = BossName(boss);
                lastAnnounced = now;
            }
            if (state == 2)
            {
                if (phase is null || phase.Boss != boss) phase = new Running(boss, max, now);
                if (current < phase.Current) drops.Add((now, boss, phase.Current - current));
                phase.Current = current;
            }
            else if (phase?.Boss == boss && phase.Current < phase.Max)
            {
                // Zeit abgelaufen, Leiste zurückgesetzt = verfehlt
                Resolve(now, force: true);
                finished = Complete(phase, now);
                phase = null;
            }
            else if (phase?.Boss == boss) phase = null;   // nie angegriffen
        }
    }

    /// <summary>Regelmäßig aufrufen: ordnet Leisten-Abnahmen den Treffern im selben Moment zu.</summary>
    public void Tick() { lock (gate) Resolve(Clock(), force: false); }

    private void Resolve(DateTime now, bool force)
    {
        hits.RemoveAll(h => now - h.At > TimeSpan.FromSeconds(3));
        foreach (var drop in drops.Where(d => force || now - d.At > Match).ToList())
        {
            drops.Remove(drop);
            if (phase?.Boss != drop.Boss) continue;
            var candidates = hits.Where(h => h.Target == drop.Boss && (h.At - drop.At).Duration() <= Match).ToList();
            Attribute(drop.Amount, candidates);
            foreach (var used in candidates) hits.Remove(used);
        }
    }

    private void Attribute(int amount, List<(DateTime At, int Target, int Actor, int Skill)> candidates)
    {
        if (phase is null || candidates.Count == 0) return;
        if (candidates.Count == 1)
        {
            var only = candidates[0];
            points[only.Skill] = amount;   // dazulernen
            Add(only.Actor, only.Skill, amount);
            return;
        }
        // Welche der gleichzeitigen Treffer ergeben genau die Abnahme? (Nicht jeder Treffer senkt die Leiste.)
        var known = candidates.Where(c => points.ContainsKey(c.Skill)).Take(14).ToList();
        for (int mask = 1; mask < 1 << known.Count; mask++)
        {
            int sum = 0;
            for (int i = 0; i < known.Count; i++) if ((mask & (1 << i)) != 0) sum += points[known[i].Skill];
            if (sum != amount) continue;
            for (int i = 0; i < known.Count; i++) if ((mask & (1 << i)) != 0) Add(known[i].Actor, known[i].Skill, points[known[i].Skill]);
            return;
        }
        // Keine passende Kombination: Rest auf die unbekannten Skills, sonst auf alle gleichmäßig
        var unknown = candidates.Where(c => !points.ContainsKey(c.Skill)).ToList();
        var targets = unknown.Count > 0 ? unknown : candidates;
        foreach (var c in targets) Add(c.Actor, c.Skill, amount / targets.Count);
        if (unknown.Count == 1) points[unknown[0].Skill] = amount;
    }

    private void Add(int actor, int skill, int amount)
    {
        if (amount <= 0 || phase is null) return;
        if (!phase.Shares.TryGetValue(actor, out var skills)) phase.Shares[actor] = skills = [];
        var s = skills.GetValueOrDefault(skill);
        skills[skill] = (s.Points + amount, s.Count + 1);
    }

    private StaggerPhase Complete(Running p, DateTime now)
    {
        var shares = p.Shares.Select(kv =>
        {
            var player = entities.GetPlayerEntity(kv.Key);
            var name = player is null || player.Name.StartsWith("Player_", StringComparison.Ordinal) ? "Unbekannt" : player.Name;
            return new StaggerShare(name, self.SelfId == kv.Key || player?.IsUser == true, kv.Value.Values.Sum(v => v.Points),
                kv.Value.Select(s => new StaggerSkill(SkillName(s.Key), s.Value.Points, s.Value.Count))
                    .OrderByDescending(s => s.Points).ToList());
        }).OrderByDescending(s => s.Points).ToList();
        lastEnded = (p.Boss, now);
        bool success = p.Broken || p.Current <= 0;
        // Verfehlt = die Zeit ist abgelaufen → so lang dauert eine Phase bei diesem Boss
        if (!success && now - p.Start is var length && length > TimeSpan.FromSeconds(5) && length < TimeSpan.FromSeconds(60))
            durations[p.Boss] = length;
        return new StaggerPhase(p.Boss, BossName(p.Boss), p.Max, success ? 0 : p.Current, success, p.Start, now, shares);
    }

    private string BossName(int id) =>
        entities.GetTargetMob(id) is { MobCode: > 0 } mob ? GameText.MobName(mob) : "Boss";

    private static string SkillName(int code)
    {
        var name = GameDataProvider.Instance.GetSkillOrDefault(code)?.Name;
        return string.IsNullOrWhiteSpace(name) || name.StartsWith("Unknown", StringComparison.Ordinal) ? "?" : name;
    }

    public static bool TryParse(byte[] data, out int boss, out int kind, out int max, out int current, out int state)
    {
        boss = kind = max = current = state = 0;
        int pos = 0;
        if (!ReadVarInt(data, ref pos, out _)) return false;
        pos += 2;
        if (!ReadVarInt(data, ref pos, out boss)) return false;
        if (pos + 2 > data.Length) return false;
        kind = data[pos];
        if (kind == 0) return true;
        if (pos + 11 > data.Length) return false;
        max = BitConverter.ToInt32(data, pos + 2);
        current = BitConverter.ToInt32(data, pos + 6);
        state = data[pos + 10];
        return max > 0 && current >= 0 && current <= max * 2;
    }

    private static bool ReadVarInt(byte[] data, ref int pos, out int value)
    {
        value = 0;
        for (int shift = 0; shift < 35 && pos < data.Length; shift += 7)
        {
            byte b = data[pos++];
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) return true;
        }
        return false;
    }
}
