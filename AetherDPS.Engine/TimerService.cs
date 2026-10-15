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

    /// <summary>Stand der mitgelieferten Vorlagen (1 = EU-Termine aus 0.6.0).</summary>
    public int DefaultsVersion { get; set; }
    public Dictionary<string, int> BossRespawnMinutes { get; set; } = [];
    public HashSet<string> IgnoredBosses { get; set; } = [];
    public List<CustomTimer> Custom { get; set; } = [];
    public List<BossKill> Kills { get; set; } = [];

    // Timer-Overlay
    public bool OverlayVisible { get; set; } = true;

    /// <summary>„always“ = immer, „soon“ = nur kurz vor einem Termin, „off“ = aus. Leer = aus OverlayVisible übernehmen.</summary>
    public string? OverlayMode { get; set; }

    /// <summary>Modus „soon“: so viele Minuten vorher erscheint der Timer.</summary>
    public int OverlaySoonMinutes { get; set; } = 5;

    /// <summary>Spalten der Timer-Gruppe (1–4).</summary>
    public int OverlayColumns { get; set; } = 1;
    public double? OverlayLeft { get; set; }
    public double? OverlayTop { get; set; }
    public double? OverlayWidth { get; set; }
    public double? OverlayHeight { get; set; }

    /// <summary>Alt (bis 0.7.0): einzeln gelöste Timer mit Position [x, y] – wird in <see cref="Groups"/> übernommen.</summary>
    public Dictionary<string, double[]> Detached { get; set; } = [];

    /// <summary>Ab 0.7.1: jeder Timer ist eine Pill; Pills stehen allein oder in Gruppen (Position in Bildschirm-Pixeln).</summary>
    public List<TimerGroup> Groups { get; set; } = [];

    /// <summary>1 = alte Gruppe/Leisten schon in Pills übernommen.</summary>
    public int PillsVersion { get; set; }

    /// <summary>Pills gesperrt: Klicks gehen ins Spiel (entsperren im Reiter Timer oder im Tray).</summary>
    public bool OverlayLocked { get; set; }
}

/// <summary>Pills, die zusammen verschoben werden: je Timer eine Zelle (Spalte, Zeile) im Raster.</summary>
public sealed class TimerGroup
{
    public double X { get; set; }
    public double Y { get; set; }
    public List<TimerCell> Cells { get; set; } = [];
}

public sealed record TimerCell(string Name, int Col, int Row);

/// <summary>
/// Gruppieren nach Augenmaß: aus der Lage lose hingezogener Pills ein Raster ableiten. Nebeneinander → eine Zeile,
/// untereinander → eine Spalte, 2 oben + 2 unten → 2×2. Mittelpunkte, die weniger als eine halbe Pill auseinander
/// liegen, landen in derselben Spalte bzw. Zeile.
/// </summary>
public static class PillLayout
{
    public static List<TimerCell> Infer(IReadOnlyList<(string Name, double X, double Y)> centers, double width, double height)
    {
        if (centers.Count == 0) return [];
        var cols = Bands(centers.Select(c => c.X), width / 2);
        var rows = Bands(centers.Select(c => c.Y), height / 2);
        var cells = new List<TimerCell>();
        var taken = new HashSet<(int, int)>();
        foreach (var c in centers.OrderBy(c => c.Y).ThenBy(c => c.X))
        {
            int col = Band(cols, c.X), row = Band(rows, c.Y);
            // zwei Pills fast übereinander → die zweite rückt in die nächste freie Zelle der Zeile
            while (!taken.Add((col, row))) col++;
            cells.Add(new TimerCell(c.Name, col, row));
        }
        return cells.OrderBy(c => c.Row).ThenBy(c => c.Col).ToList();
    }

    /// <summary>Zeilenweise mit fester Spaltenzahl (für „Spalten: n“).</summary>
    public static List<TimerCell> Grid(IEnumerable<string> names, int columns) =>
        names.Select((n, i) => new TimerCell(n, i % Math.Max(1, columns), i / Math.Max(1, columns))).ToList();

    private static List<double> Bands(IEnumerable<double> values, double tolerance)
    {
        var bands = new List<double>();
        foreach (var v in values.Order())
            if (bands.Count == 0 || v - bands[^1] > tolerance) bands.Add(v);
        return bands;
    }

    private static int Band(List<double> bands, double v)
    {
        int best = 0;
        for (int i = 1; i < bands.Count; i++)
            if (Math.Abs(bands[i] - v) < Math.Abs(bands[best] - v)) best = i;
        return best;
    }
}

public enum TimerKind { Schedule, Boss, Custom, FieldBoss }

/// <param name="State">Statt eines Countdowns anzeigen (z. B. Feldboss „lebt“).</param>
public sealed record TimerItem(TimerKind Kind, string Name, string Category, DateTime? DueAt, TimeSpan? Remaining,
    string? Detail, bool ShowInOverlay, bool Unconfirmed, string? State = null);

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

    private readonly FieldBosses fieldBosses;

    public TimerService(MeterOptions options, EntityTracker entities, ZoneTracker zones, FieldBosses fieldBosses)
    {
        this.zones = zones;
        this.fieldBosses = fieldBosses;
        fieldBosses.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        file = Path.Combine(options.DataDir, "timer.json");
        this.entities = entities;
        Settings = Load(file);
        if (Settings.DefaultsVersion < 2)
        {
            // Ab 0.6.3 nur noch die festen Termine aus dem Katalog (an/aus). An/aus und Erinnerung werden übernommen.
            var old = Settings.Schedules ?? [];
            var builtins = ScheduleEntry.Builtins();
            foreach (var b in builtins)
            {
                var match = old.FirstOrDefault(o => o.Id == b.Id)
                    ?? (b.Id == "rift" ? old.FirstOrDefault(o => o.Category == "Riss" && o.IntervalHours > 0) : null)
                    ?? old.FirstOrDefault(o => o.Name == b.Name);
                if (match is not null)
                {
                    b.Enabled = match.Enabled;
                    b.Reminder = match.Reminder;
                    b.ShowInOverlay = match.ShowInOverlay;
                }
                else if (b.Id == "rift")
                {
                    b.Reminder = Settings.RiftReminder;
                }
            }
            Settings.Schedules = builtins;
            Settings.DefaultsVersion = 2;
            if (File.Exists(file)) Save();
        }
        entities.EntityDied += OnEntityDied;
    }

    private void OnEntityDied(int entityId)
    {
        var mob = entities.GetTargetMob(entityId);
        if (mob is null || mob.MobCode <= 0 || !mob.IsBoss || mob.IsDummy) return;
        // Bekannte Feldbosse führt FieldBosses (mit den echten Zeiten des Servers)
        if (fieldBosses.IsFieldBoss(mob.MobCode)) return;
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
                list.Add(new TimerItem(TimerKind.Schedule, s.DisplayName, s.Category, next, next - now, s.Describe(),
                    s.ShowInOverlay, s.Unconfirmed));
            }

            foreach (var kill in Settings.Kills.OrderByDescending(k => k.KilledAt))
            {
                if (Settings.BossRespawnMinutes.TryGetValue(kill.Name, out var minutes) && minutes > 0)
                {
                    var due = kill.KilledAt.AddMinutes(minutes);
                    list.Add(new TimerItem(TimerKind.Boss, kill.Name, "Feldboss", due, due > now ? due - now : TimeSpan.Zero,
                        Loc.T("timer.killed_at", kill.KilledAt), true, false));
                }
                else
                {
                    // ohne Respawn-Zeit nur im Reiter „Timer“ (zum Eintragen), nicht im Overlay
                    list.Add(new TimerItem(TimerKind.Boss, kill.Name, "Feldboss", null, null,
                        Loc.T("timer.killed_set_respawn", kill.KilledAt), false, false));
                }
            }

            foreach (var custom in Settings.Custom)
                list.Add(new TimerItem(TimerKind.Custom, custom.Name, "Custom", custom.EndsAt,
                    custom.EndsAt is DateTime e ? (e > now ? e - now : TimeSpan.Zero) : null, Loc.T("timer.minutes", custom.Minutes), true, false));
        }

        // Feldbosse mit Stern: echte Zeiten vom Server (Karte im Spiel geöffnet)
        foreach (var fb in fieldBosses.Views(now).Where(v => v.Favorite))
        {
            var (due, state) = fb.Status switch
            {
                FieldBossStatus.Respawning or FieldBossStatus.Scheduled => (fb.Time, (string?)null),
                FieldBossStatus.Alive => ((DateTime?)null, Loc.T("fb.alive")),
                FieldBossStatus.ProbablyUp => (null, Loc.T("fb.probably_up")),
                _ => (null, Loc.T("fb.unknown_short")),
            };
            list.Add(new TimerItem(TimerKind.FieldBoss, fb.Boss.Name, "Feldboss", due, due is DateTime d ? d - now : null,
                fb.Boss.Map.Name + (fb.Estimated ? " · " + Loc.T("fb.estimated") : ""), true, false, state));
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
                    reminders.Add((s.DisplayName, Loc.T("timer.reminder", next, Math.Ceiling((next - now).TotalMinutes))));
            }

            foreach (var kill in Settings.Kills)
            {
                if (!Settings.BossRespawnMinutes.TryGetValue(kill.Name, out var minutes) || minutes <= 0) continue;
                var due = kill.KilledAt.AddMinutes(minutes);
                if (due - now <= lead && due > now.AddMinutes(-1) && remindedKeys.Add($"boss:{kill.Name}:{due:O}"))
                    reminders.Add((kill.Name, Loc.T("timer.appears_at", due)));
            }

            foreach (var fb in fieldBosses.Views(now).Where(v => v.Favorite && v.Time is not null
                         && v.Status is FieldBossStatus.Respawning or FieldBossStatus.Scheduled))
            {
                var due = fb.Time!.Value;
                if (due - now <= lead && due > now.AddMinutes(-1) && remindedKeys.Add($"fb:{fb.Boss.Npc}:{due:O}"))
                    reminders.Add((fb.Boss.Name, Loc.T("timer.appears_at", due)));
            }

            foreach (var custom in Settings.Custom)
            {
                if (custom.EndsAt is DateTime e && e <= now && e > now.AddMinutes(-1) && remindedKeys.Add($"custom:{custom.Name}:{e:O}"))
                    reminders.Add((custom.Name, Loc.T("timer.expired")));
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

    /// <summary>Festen Termin an-/abschalten bzw. Erinnerung setzen.</summary>
    public void SetSchedule(ScheduleEntry entry, bool enabled, bool reminder)
    {
        lock (gate)
        {
            entry.Enabled = enabled;
            entry.Reminder = reminder;
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

    /// <summary>Pill entfernen: eigener Timer/Bosskill weg, fester Termin aus, Feldboss ohne Stern.</summary>
    public void RemoveFromOverlay(TimerKind kind, string name)
    {
        if (kind == TimerKind.FieldBoss)
        {
            var boss = fieldBosses.Maps.SelectMany(m => m.Bosses).FirstOrDefault(b => b.Name == name);
            if (boss is not null) fieldBosses.SetFavorite(boss.Npc, false);
            return;
        }
        if (kind == TimerKind.Schedule)
        {
            lock (gate)
            {
                foreach (var s in Settings.Schedules!.Where(s => s.DisplayName == name || s.Name == name)) s.Enabled = false;
            }
            Save();
            return;
        }
        Remove(kind, name);
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
