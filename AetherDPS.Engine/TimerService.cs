using System.Text.Encodings.Web;
using System.Text.Json;
using AionDpsMeter.Services.Services.Entity;

namespace AetherDPS.Engine;

public sealed class CustomTimer
{
    public string Name { get; set; } = "";
    public int Minutes { get; set; }
    public DateTime? EndsAt { get; set; }
}

public sealed record BossKill(string Name, DateTime KilledAt);

public sealed class TimerSettings
{
    // alt (vor Zeitplänen) – wird beim ersten Laden in Schedules übernommen
    public string RiftAnchor { get; set; } = "02:00";
    public double RiftIntervalHours { get; set; } = 3;
    public bool RiftReminder { get; set; } = true;

    public int ReminderMinutes { get; set; } = 5;
    public List<ScheduleEntry>? Schedules { get; set; }
    public Dictionary<string, int> BossRespawnMinutes { get; set; } = [];
    public HashSet<string> IgnoredBosses { get; set; } = [];
    public List<CustomTimer> Custom { get; set; } = [];
    public List<BossKill> Kills { get; set; } = [];

    // Timer-Overlay
    public bool OverlayVisible { get; set; } = true;
    public double? OverlayLeft { get; set; }
    public double? OverlayTop { get; set; }
    public double? OverlayWidth { get; set; }
    public double? OverlayHeight { get; set; }

    /// <summary>Einzeln gelöste Timer (eigene Leiste) mit Position [x, y] – Schlüssel = Name.</summary>
    public Dictionary<string, double[]> Detached { get; set; } = [];
}

public enum TimerKind { Schedule, Boss, Custom }

public sealed record TimerItem(TimerKind Kind, string Name, string Category, DateTime? DueAt, TimeSpan? Remaining,
    string? Detail, bool ShowInOverlay, bool Unconfirmed);

/// <summary>
/// Timer: feste Zeitpläne (Risse, Weltbosse, Events – selbst anlegbar), Feldbosse (Tod wird
/// aus dem Netzwerk erkannt, Respawn-Dauer stellt der Nutzer je Boss einmal ein) und eigene
/// Countdown-Timer. Feuert Erinnerungen.
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

    public IReadOnlyList<ScheduleEntry> Schedules { get { lock (gate) return Settings.Schedules!.ToList(); } }

    /// <summary>Erinnerung (Titel, Text). Wird auf dem aufrufenden Thread von <see cref="Tick"/> ausgelöst.</summary>
    public event Action<string, string>? Reminder;
    public event EventHandler? Changed;

    private readonly ZoneTracker zones;

    public TimerService(MeterOptions options, EntityTracker entities, ZoneTracker zones)
    {
        this.zones = zones;
        file = Path.Combine(options.DataDir, "timer.json");
        this.entities = entities;
        Settings = Load(file);
        if (Settings.Schedules is null)
        {
            Settings.Schedules = ScheduleEntry.Defaults(Settings.RiftAnchor, Settings.RiftIntervalHours, Settings.RiftReminder);
            if (File.Exists(file)) Save();
        }
        entities.EntityDied += OnEntityDied;
    }

    private void OnEntityDied(int entityId)
    {
        var mob = entities.GetTargetMob(entityId);
        if (mob is null || mob.MobCode <= 0 || !mob.IsBoss || mob.IsDummy) return;
        // Bosse im Dungeon sind keine Feldbosse – die kommen nicht wieder (14.10.: Phantom-Lakshmi u. a. im Overlay)
        if (zones.InInstance) return;
        var name = GameText.MobName(mob);
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

    /// <summary>Nächster Termin im Takt ab <paramref name="now"/> (Startpunkt + n × Intervall).</summary>
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
            foreach (var s in Settings.Schedules!.Where(s => s.Enabled))
            {
                var next = s.Next(now);
                list.Add(new TimerItem(TimerKind.Schedule, s.Name, s.Category, next, next - now, s.Describe(),
                    s.ShowInOverlay, s.Unconfirmed));
            }

            foreach (var kill in Settings.Kills.OrderByDescending(k => k.KilledAt))
            {
                if (Settings.BossRespawnMinutes.TryGetValue(kill.Name, out var minutes) && minutes > 0)
                {
                    var due = kill.KilledAt.AddMinutes(minutes);
                    list.Add(new TimerItem(TimerKind.Boss, kill.Name, "Feldboss", due, due > now ? due - now : TimeSpan.Zero,
                        $"getötet {kill.KilledAt:HH:mm}", true, false));
                }
                else
                {
                    // ohne Respawn-Zeit nur im Reiter „Timer“ (zum Eintragen), nicht im Overlay
                    list.Add(new TimerItem(TimerKind.Boss, kill.Name, "Feldboss", null, null,
                        $"getötet {kill.KilledAt:HH:mm} – Respawn-Zeit eintragen", false, false));
                }
            }

            foreach (var custom in Settings.Custom)
                list.Add(new TimerItem(TimerKind.Custom, custom.Name, "Eigener Timer", custom.EndsAt,
                    custom.EndsAt is DateTime e ? (e > now ? e - now : TimeSpan.Zero) : null, $"{custom.Minutes} Min.", true, false));
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
            foreach (var s in Settings.Schedules!.Where(s => s.Enabled && s.Reminder))
            {
                if (s.Next(now) is DateTime next && next - now <= lead && remindedKeys.Add($"plan:{s.Name}:{next:O}"))
                    reminders.Add((s.Name, $"um {next:HH:mm} (in {Math.Ceiling((next - now).TotalMinutes)} Min.)"));
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

    public void AddSchedule(ScheduleEntry entry)
    {
        lock (gate) Settings.Schedules!.Add(entry);
        Save();
    }

    public void ReplaceSchedule(ScheduleEntry old, ScheduleEntry updated)
    {
        lock (gate)
        {
            int i = Settings.Schedules!.IndexOf(old);
            if (i >= 0) Settings.Schedules[i] = updated;
            else Settings.Schedules.Add(updated);
        }
        Save();
    }

    public void RemoveSchedule(ScheduleEntry entry)
    {
        lock (gate) Settings.Schedules!.Remove(entry);
        Save();
    }

    public bool IsDetached(string name) { lock (gate) return Settings.Detached.ContainsKey(name); }

    public double[]? DetachedPosition(string name) { lock (gate) return Settings.Detached.GetValueOrDefault(name); }

    /// <summary>Timer als eigene Leiste lösen (Position null = neben der Gruppe) oder zurück in die Gruppe.</summary>
    public void SetDetached(string name, bool detached, double[]? position = null)
    {
        lock (gate)
        {
            if (detached) Settings.Detached[name] = position ?? Settings.Detached.GetValueOrDefault(name) ?? [];
            else Settings.Detached.Remove(name);
        }
        Save();
    }

    public void SetOverlayFlag(TimerKind kind, string name, bool show)
    {
        if (kind != TimerKind.Schedule) return;
        lock (gate)
        {
            foreach (var s in Settings.Schedules!.Where(s => s.Name == name)) s.ShowInOverlay = show;
        }
        Save();
    }

    public void Remove(TimerKind kind, string name)
    {
        lock (gate)
        {
            if (kind == TimerKind.Custom) Settings.Custom.RemoveAll(c => c.Name == name);
            if (kind == TimerKind.Boss) Settings.Kills.RemoveAll(k => k.Name == name);
            if (kind == TimerKind.Schedule) Settings.Schedules!.RemoveAll(s => s.Name == name);
        }
        Save();
    }

    public void Save()
    {
        SaveQuiet();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Nur speichern (z. B. Fensterpositionen), ohne die Timer-Listen neu aufzubauen.</summary>
    public void SaveQuiet()
    {
        try
        {
            lock (gate) File.WriteAllText(file, JsonSerializer.Serialize(Settings, Json));
        }
        catch (IOException)
        {
            // kurz gesperrt – beim nächsten Speichern klappt es
        }
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
