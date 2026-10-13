using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Services.Services.Timed;

namespace Aion2Meter.Engine;

public enum LiveKind { Buff, Cooldown }

public sealed record LiveItem(LiveKind Kind, string Name, TimeSpan Remaining, TimeSpan Duration)
{
    public double Fraction => Duration.TotalMilliseconds > 0 ? Remaining.TotalMilliseconds / Duration.TotalMilliseconds : 0;
}

public sealed record LiveDiagnostics(int BuffEvents, int OwnBuffEvents, int CooldownEvents, string? Self, bool SelfGuessed);

/// <summary>
/// Buff-Leiste: eigene Buffs (Ziel = dein Charakter) und eigene Abklingzeiten, so wie das
/// Spiel sie schickt. Nur Anzeige – liest weiterhin nur mit.
/// Den eigenen Charakter kennt Upstream erst nach Login/Teleport. Bis dahin wird er
/// erraten: Abklingzeit-Pakete kommen nur für DEINE Skills – wer zuletzt mit genau
/// diesem Skill Schaden gemacht hat, bist du.
/// </summary>
public sealed class LiveTimers
{
    public static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(3);

    private readonly EntityTracker entities;
    private readonly Lock gate = new();
    private readonly Dictionary<(LiveKind, string), (DateTime Start, TimeSpan Duration)> items = [];
    private readonly Dictionary<int, (int EntityId, DateTime At)> lastUserOfSkill = [];
    private readonly Dictionary<int, int> selfVotes = [];
    private readonly List<BuffEvent> pendingBuffs = [];
    private int buffEvents, ownBuffEvents, cooldownEvents;

    public LiveTimers(CombatSessionManager sessions, EntityTracker entities)
    {
        this.entities = entities;
        sessions.BuffRecorded += OnBuff;
        sessions.SkillCooldownRecorded += OnCooldown;
        sessions.DamageRecorded += OnDamage;
    }

    /// <summary>Eigener Charakter: sicher (Upstream) oder geraten (Abklingzeit ↔ Schaden).</summary>
    private int? SelfId(out bool guessed)
    {
        guessed = false;
        var sure = selfVotes.Keys.Concat(lastUserOfSkill.Values.Select(v => v.EntityId)).Distinct()
            .FirstOrDefault(id => entities.IsCurrentPlayer(id));
        if (sure != 0) return sure;
        if (selfVotes.Count == 0) return null;
        guessed = true;
        return selfVotes.MaxBy(v => v.Value).Key;
    }

    private bool IsSelf(int entityId)
    {
        if (entities.IsCurrentPlayer(entityId)) return true;
        return SelfId(out _) == entityId;
    }

    private void OnDamage(PlayerDamage hit)
    {
        if (hit.Skill is null || hit.SourceSummon is not null) return;
        lock (gate) lastUserOfSkill[hit.Skill.Id] = (hit.SourceEntity.Id, hit.DateTime);
    }

    private void OnBuff(BuffEvent buff)
    {
        var duration = TimeSpan.FromMilliseconds(buff.DurationMs);
        if (duration < MinDuration || string.IsNullOrWhiteSpace(buff.BuffName)) return;
        lock (gate)
        {
            buffEvents++;
            if (IsSelf(buff.EntityId))
            {
                ownBuffEvents++;
                items[(LiveKind.Buff, buff.BuffName)] = (buff.AppliedAt, duration);
            }
            else
            {
                // Vielleicht bist du das, nur noch nicht erkannt – kurz merken
                pendingBuffs.RemoveAll(b => b.AppliedAt + TimeSpan.FromMilliseconds(b.DurationMs) <= DateTime.Now);
                if (pendingBuffs.Count < 200) pendingBuffs.Add(buff);
            }
        }
    }

    private void OnCooldown(TimedEvent cd)
    {
        lock (gate)
        {
            cooldownEvents++;
            // Wer hat diesen Skill gerade benutzt? → Stimme für „das bin ich“
            if (lastUserOfSkill.TryGetValue((int)cd.Id, out var user) && DateTime.Now - user.At < TimeSpan.FromSeconds(10))
            {
                selfVotes[user.EntityId] = selfVotes.GetValueOrDefault(user.EntityId) + 1;
                // gemerkte Buffs dieses Charakters nachtragen
                foreach (var b in pendingBuffs.Where(b => b.EntityId == user.EntityId && IsSelf(b.EntityId)).ToList())
                {
                    items[(LiveKind.Buff, b.BuffName)] = (b.AppliedAt, TimeSpan.FromMilliseconds(b.DurationMs));
                    pendingBuffs.Remove(b);
                    ownBuffEvents++;
                }
            }
            if (cd.Duration >= MinDuration && !string.IsNullOrWhiteSpace(cd.Name))
                items[(LiveKind.Cooldown, cd.Name)] = (DateTime.Now, cd.Duration);
        }
    }

    public IReadOnlyList<LiveItem> Active(DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        lock (gate)
        {
            foreach (var key in items.Where(i => i.Value.Start + i.Value.Duration <= now).Select(i => i.Key).ToList())
                items.Remove(key);
            return items
                .Select(i => new LiveItem(i.Key.Item1, i.Key.Item2, i.Value.Start + i.Value.Duration - now, i.Value.Duration))
                .OrderBy(i => i.Kind).ThenBy(i => i.Remaining)
                .ToList();
        }
    }

    public LiveDiagnostics Diagnostics()
    {
        lock (gate)
        {
            var self = SelfId(out bool guessed);
            string? name = self is int id ? entities.GetPlayerEntity(id)?.Name ?? $"Spieler {id}" : null;
            return new LiveDiagnostics(buffEvents, ownBuffEvents, cooldownEvents, name, guessed);
        }
    }

    public void Clear()
    {
        lock (gate) items.Clear();
    }
}
