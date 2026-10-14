using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.Services.Session;

namespace Aion2Meter.Engine;

/// <summary>
/// Warnt, wenn ein langer eigener Buff (Essen, Rollen – mindestens 5 Minuten Laufzeit) ausläuft
/// und nicht erneuert wurde. Kurze Kampf-Buffs werden ignoriert, sonst käme ständig etwas.
/// </summary>
public sealed class BuffWatcher
{
    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(5);

    private readonly SelfIdentity self;
    private readonly Lock gate = new();
    private readonly Dictionary<int, (string Name, DateTime Expires)> running = [];

    public BuffWatcher(CombatSessionManager sessions, SelfIdentity self)
    {
        this.self = self;
        sessions.BuffRecorded += OnBuff;
    }

    /// <summary>Name des abgelaufenen Buffs.</summary>
    public event Action<string>? Expired;

    public int Watched { get { lock (gate) return running.Count; } }

    private void OnBuff(BuffEvent buff) => Add(buff, DateTime.Now);

    public void Add(BuffEvent buff, DateTime now)
    {
        if (buff.EntityId != self.SelfId || buff.DurationMs < MinDuration.TotalMilliseconds) return;
        var name = string.IsNullOrWhiteSpace(buff.BuffName) ? $"Buff {buff.BuffId}" : buff.BuffName;
        lock (gate) running[buff.BuffId] = (name, now.AddMilliseconds(buff.DurationMs));
    }

    /// <summary>Einmal pro Sekunde aufrufen.</summary>
    public void Tick(DateTime now)
    {
        List<string> expired;
        lock (gate)
        {
            expired = running.Where(r => r.Value.Expires <= now).Select(r => r.Value.Name).ToList();
            foreach (var id in running.Where(r => r.Value.Expires <= now).Select(r => r.Key).ToList()) running.Remove(id);
        }
        foreach (var name in expired) Expired?.Invoke(name);
    }
}
