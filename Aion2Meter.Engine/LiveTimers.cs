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

/// <summary>
/// Buff-Leiste: eigene Buffs (Ziel = dein Charakter) und eigene Abklingzeiten, so wie das
/// Spiel sie schickt. Nur Anzeige – liest weiterhin nur mit.
/// </summary>
public sealed class LiveTimers
{
    public static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(3);

    private readonly EntityTracker entities;
    private readonly Lock gate = new();
    private readonly Dictionary<(LiveKind, string), (DateTime Start, TimeSpan Duration)> items = [];

    public LiveTimers(CombatSessionManager sessions, EntityTracker entities)
    {
        this.entities = entities;
        sessions.BuffRecorded += OnBuff;
        sessions.SkillCooldownRecorded += OnCooldown;
    }

    private void OnBuff(BuffEvent buff)
    {
        if (!entities.IsCurrentPlayer(buff.EntityId)) return;
        var duration = TimeSpan.FromMilliseconds(buff.DurationMs);
        if (duration < MinDuration || string.IsNullOrWhiteSpace(buff.BuffName)) return;
        lock (gate) items[(LiveKind.Buff, buff.BuffName)] = (DateTime.Now, duration);
    }

    private void OnCooldown(TimedEvent cd)
    {
        if (cd.Duration < MinDuration || string.IsNullOrWhiteSpace(cd.Name)) return;
        lock (gate) items[(LiveKind.Cooldown, cd.Name)] = (DateTime.Now, cd.Duration);
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

    public void Clear()
    {
        lock (gate) items.Clear();
    }
}
