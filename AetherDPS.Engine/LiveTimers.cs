using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;

namespace AetherDPS.Engine;

public sealed record LiveItem(string Name, TimeSpan Remaining, TimeSpan Duration)
{
    public double Fraction => Duration.TotalMilliseconds > 0 ? Remaining.TotalMilliseconds / Duration.TotalMilliseconds : 0;
}

public sealed record LiveDiagnostics(int BuffEvents, int OwnBuffEvents, string? Self, bool SelfGuessed, int CooldownPackets);

/// <summary>
/// Buff-Leiste: aktive Buffs auf deinem Charakter, so wie das Spiel sie schickt.
/// (Abklingzeiten wurden wieder entfernt – das Spiel verkürzt sie situativ, die Anzeige
/// war deshalb unzuverlässig.) Buffs, die ankommen, bevor dein Charakter erkannt ist,
/// werden nachgetragen, sobald er feststeht (siehe <see cref="SelfIdentity"/>).
/// </summary>
public sealed class LiveTimers
{
    public static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(3);

    private readonly EntityTracker entities;
    private readonly SelfIdentity self;
    private readonly Lock gate = new();
    private readonly Dictionary<string, (DateTime Start, TimeSpan Duration)> items = [];
    private readonly List<BuffEvent> pending = [];
    private int buffEvents, ownBuffEvents;

    public LiveTimers(CombatSessionManager sessions, EntityTracker entities, SelfIdentity self)
    {
        this.entities = entities;
        this.self = self;
        sessions.BuffRecorded += OnBuff;
    }

    private bool IsSelf(int entityId) => entities.IsCurrentPlayer(entityId) || self.SelfId == entityId;

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
                items[buff.BuffName] = (buff.AppliedAt, duration);
            }
            else if (pending.Count < 300)
            {
                pending.Add(buff);   // vielleicht du, nur noch nicht erkannt
            }
        }
    }

    public IReadOnlyList<LiveItem> Active(DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        lock (gate)
        {
            pending.RemoveAll(b => b.AppliedAt.AddMilliseconds(b.DurationMs) <= now);
            foreach (var b in pending.Where(b => IsSelf(b.EntityId)).ToList())
            {
                items[b.BuffName] = (b.AppliedAt, TimeSpan.FromMilliseconds(b.DurationMs));
                pending.Remove(b);
                ownBuffEvents++;
            }
            foreach (var key in items.Where(i => i.Value.Start + i.Value.Duration <= now).Select(i => i.Key).ToList())
                items.Remove(key);
            return items
                .Select(i => new LiveItem(i.Key, i.Value.Start + i.Value.Duration - now, i.Value.Duration))
                .OrderBy(i => i.Remaining)
                .ToList();
        }
    }

    public LiveDiagnostics Diagnostics()
    {
        lock (gate) return new LiveDiagnostics(buffEvents, ownBuffEvents, self.Name, self.Guessed, self.CooldownPackets);
    }
}
