using System.Text.Encodings.Web;
using System.Text.Json;
using AionDpsMeter.Services.Services.Entity;

namespace Aion2Meter.Engine;

public sealed class CustomTimer
{
    public string Name { get; set; } = "";
    public int Minutes { get; set; }
    public DateTime? EndsAt { get; set; }
}

public sealed record BossKill(string Name, DateTime KilledAt);

public sealed class TimerSettings
{
    /// <summary>Uhrzeit eines bekannten Riss-Starts (lokale Zeit) – von dort aus im Takt weiter.</summary>
    public string RiftAnchor { get; set; } = "02:00";
    public double RiftIntervalHours { get; set; } = 3;
    public int ReminderMinutes { get; set; } = 5;
    public bool RiftReminder { get; set; } = true;
    public Dictionary<string, int> BossRespawnMinutes { get; set; } = [];
    public HashSet<string> IgnoredBosses { get; set; } = [];
    public List<CustomTimer> Custom { get; set; } = [];
    public List<BossKill> Kills { get; set; } = [];
}

public enum TimerKind { Rift, Boss, Custom }

public sealed record TimerItem(TimerKind Kind, string Name, DateTime? DueAt, TimeSpan? Remaining, string? Detail);

/// <summary>
/// Timer: Risse nach Zeitplan, Feldbosse (Tod wird aus dem Netzwerk erkannt, Respawn-Dauer
/// stellt der Nutzer je Boss einmal ein) und eigene Timer. Feuert Erinnerungen.
/// </summary>
public sealed class TimerService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string file;
    private readonly EntityTracker entities;
    private readonly Lock gate = new();
    private readonly HashSet<string> remindedKeys = [];

    public TimerSettings Settings { get; }

    /// <summary>Erinnerung (Titel, Text). Wird auf dem aufrufenden Thread von <see cref="Tick"/> ausgelöst.</summary>
    public event Action<string, string>? Reminder;
    public event EventHandler? Changed;

    public TimerService(MeterOptions options, EntityTracker entities)
    {
        file = Path.Combine(options.DataDir, "timer.json");
        this.entities = entities;
        Settings = Load(file);
        entities.EntityDied += OnEntityDied;
    }

    private void OnEntityDied(int entityId)
    {
        var mob = entities.GetTargetMob(entityId);
        if (mob is null || mob.MobCode <= 0 || !mob.IsBoss || mob.IsDummy) return;
        var name = GermanGameData.MobName(mob);
        lock (gate)
        {
            if (Settings.IgnoredBosses.Contains(name)) return;
            Settings.Kills.RemoveAll(k => k.Name == name);
            Settings.Kills.Add(new BossKill(name, DateTime.Now));
            if (Settings.Kills.Count > 40) Settings.Kills.RemoveAt(0);
            remindedKeys.RemoveWhere(k => k.StartsWith($"boss:{name}:", StringComparison.Ordinal));
        }
        Save();
    }

    /// <summary>Nächster Riss-Start ab <paramref name="now"/>.</summary>
    public static DateTime NextRift(DateTime now, string anchor, double intervalHours)
    {
        if (!TimeSpan.TryParse(anchor, out var time)) time = TimeSpan.FromHours(2);
        var interval = TimeSpan.FromHours(Math.Max(0.25, intervalHours));
        var start = now.Date + time;
        while (start > now) start -= interval;
        while (start <= now) start += interval;
        return start;
    }

    public IReadOnlyList<TimerItem> Items(DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        var list = new List<TimerItem>();
        lock (gate)
        {
            var rift = NextRift(now, Settings.RiftAnchor, Settings.RiftIntervalHours);
            list.Add(new TimerItem(TimerKind.Rift, "Raumzeit-Riss", rift, rift - now, null));

            foreach (var kill in Settings.Kills.OrderByDescending(k => k.KilledAt))
            {
                if (Settings.BossRespawnMinutes.TryGetValue(kill.Name, out var minutes) && minutes > 0)
                {
                    var due = kill.KilledAt.AddMinutes(minutes);
                    list.Add(new TimerItem(TimerKind.Boss, kill.Name, due, due > now ? due - now : TimeSpan.Zero, $"getötet {kill.KilledAt:HH:mm}"));
                }
                else
                {
                    list.Add(new TimerItem(TimerKind.Boss, kill.Name, null, null, $"getötet {kill.KilledAt:HH:mm} – Respawn-Zeit eintragen"));
                }
            }

            foreach (var custom in Settings.Custom)
                list.Add(new TimerItem(TimerKind.Custom, custom.Name, custom.EndsAt,
                    custom.EndsAt is DateTime e ? (e > now ? e - now : TimeSpan.Zero) : null, $"{custom.Minutes} Min."));
        }
        return list;
    }

    /// <summary>Regelmäßig aufrufen (z. B. jede Sekunde): löst fällige Erinnerungen aus.</summary>
    public void Tick(DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        var reminders = new List<(string, string)>();
        lock (gate)
        {
            var lead = TimeSpan.FromMinutes(Math.Max(0, Settings.ReminderMinutes));
            if (Settings.RiftReminder)
            {
                var rift = NextRift(now, Settings.RiftAnchor, Settings.RiftIntervalHours);
                if (rift - now <= lead && remindedKeys.Add($"rift:{rift:O}"))
                    reminders.Add(("Raumzeit-Riss", $"öffnet um {rift:HH:mm} (in {Math.Ceiling((rift - now).TotalMinutes)} Min.)"));
            }

            foreach (var kill in Settings.Kills)
            {
                if (!Settings.BossRespawnMinutes.TryGetValue(kill.Name, out var minutes) || minutes <= 0) continue;
                var due = kill.KilledAt.AddMinutes(minutes);
                if (due - now <= lead && due > now.AddMinutes(-1) && remindedKeys.Add($"boss:{kill.Name}:{due:O}"))
                    reminders.Add((kill.Name, $"erscheint um {due:HH:mm}"));
            }

            foreach (var custom in Settings.Custom)
            {
                if (custom.EndsAt is DateTime e && e <= now && e > now.AddMinutes(-1) && remindedKeys.Add($"custom:{custom.Name}:{e:O}"))
                    reminders.Add((custom.Name, "Timer abgelaufen"));
            }
        }
        foreach (var (title, text) in reminders) Reminder?.Invoke(title, text);
    }

    public void SetBossRespawn(string boss, int minutes)
    {
        lock (gate) Settings.BossRespawnMinutes[boss] = minutes;
        Save();
    }

    public void IgnoreBoss(string boss)
    {
        lock (gate)
        {
            Settings.IgnoredBosses.Add(boss);
            Settings.Kills.RemoveAll(k => k.Name == boss);
        }
        Save();
    }

    public void AddCustom(string name, int minutes)
    {
        if (string.IsNullOrWhiteSpace(name) || minutes <= 0) return;
        lock (gate) Settings.Custom.Add(new CustomTimer { Name = name.Trim(), Minutes = minutes });
        Save();
    }

    public void StartCustom(string name)
    {
        lock (gate)
        {
            var t = Settings.Custom.FirstOrDefault(c => c.Name == name);
            if (t is not null) t.EndsAt = DateTime.Now.AddMinutes(t.Minutes);
        }
        Save();
    }

    public void Remove(TimerKind kind, string name)
    {
        lock (gate)
        {
            if (kind == TimerKind.Custom) Settings.Custom.RemoveAll(c => c.Name == name);
            if (kind == TimerKind.Boss) Settings.Kills.RemoveAll(k => k.Name == name);
        }
        Save();
    }

    public void Save()
    {
        lock (gate) File.WriteAllText(file, JsonSerializer.Serialize(Settings, Json));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static TimerSettings Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<TimerSettings>(File.ReadAllText(path), Json) ?? new() : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }
}
