using System.Text.Encodings.Web;
using System.Text.Json;

namespace AetherDPS.Engine;

public enum ChecklistPeriod { Daily, Weekly }

public sealed class ChecklistItem
{
    public string Text { get; set; } = "";
    public ChecklistPeriod Period { get; set; }
    public DateTime? DoneAt { get; set; }
}

public sealed class ChecklistData
{
    public int DailyResetHour { get; set; } = 6;
    public DayOfWeek WeeklyResetDay { get; set; } = DayOfWeek.Wednesday;
    public int WeeklyResetHour { get; set; } = 6;
    public int ReminderMinutes { get; set; } = 30;
    public List<ChecklistItem> Items { get; set; } =
    [
        new() { Text = "Tägliche Aufträge", Period = ChecklistPeriod.Daily },
        new() { Text = "Tägliche Dungeons", Period = ChecklistPeriod.Daily },
        new() { Text = "Raumzeit-Riss", Period = ChecklistPeriod.Daily },
        new() { Text = "Wöchentliche Aufträge", Period = ChecklistPeriod.Weekly },
        new() { Text = "Wöchentliche Dungeons", Period = ChecklistPeriod.Weekly },
    ];
}

/// <summary>
/// Daily-/Weekly-Checkliste. Ein Haken gilt bis zum nächsten Reset (Zeiten einstellbar,
/// weil sie je nach Server abweichen können). Erinnert vor dem Reset an Offenes.
/// </summary>
public sealed class ChecklistService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string file;
    private readonly Lock gate = new();
    private readonly HashSet<string> reminded = [];

    public ChecklistData Data { get; }

    public event Action<string, string>? Reminder;
    public event EventHandler? Changed;

    public ChecklistService(MeterOptions options)
    {
        file = Path.Combine(options.DataDir, "checkliste.json");
        try
        {
            Data = File.Exists(file) ? JsonSerializer.Deserialize<ChecklistData>(File.ReadAllText(file), Json) ?? new() : new();
        }
        catch (JsonException)
        {
            Data = new();
        }
    }

    public static DateTime LastDailyReset(DateTime now, int hour)
    {
        var reset = now.Date.AddHours(hour);
        return reset > now ? reset.AddDays(-1) : reset;
    }

    public static DateTime LastWeeklyReset(DateTime now, DayOfWeek day, int hour)
    {
        var reset = now.Date.AddDays(-(((int)now.DayOfWeek - (int)day + 7) % 7)).AddHours(hour);
        return reset > now ? reset.AddDays(-7) : reset;
    }

    public DateTime LastReset(ChecklistPeriod period, DateTime now) => period == ChecklistPeriod.Daily
        ? LastDailyReset(now, Data.DailyResetHour)
        : LastWeeklyReset(now, Data.WeeklyResetDay, Data.WeeklyResetHour);

    public DateTime NextReset(ChecklistPeriod period, DateTime now) =>
        LastReset(period, now).AddDays(period == ChecklistPeriod.Daily ? 1 : 7);

    public bool IsDone(ChecklistItem item, DateTime? at = null) =>
        item.DoneAt is DateTime done && done >= LastReset(item.Period, at ?? DateTime.Now);

    public void SetDone(ChecklistItem item, bool done)
    {
        lock (gate) item.DoneAt = done ? DateTime.Now : null;
        Save();
    }

    public void Add(string text, ChecklistPeriod period)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        lock (gate) Data.Items.Add(new ChecklistItem { Text = text.Trim(), Period = period });
        Save();
    }

    public void Remove(ChecklistItem item)
    {
        lock (gate) Data.Items.Remove(item);
        Save();
    }

    /// <summary>Regelmäßig aufrufen: erinnert X Minuten vor dem Reset an offene Punkte.</summary>
    public void Tick(DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        foreach (var period in new[] { ChecklistPeriod.Daily, ChecklistPeriod.Weekly })
        {
            var next = NextReset(period, now);
            if (next - now > TimeSpan.FromMinutes(Data.ReminderMinutes)) continue;
            int open;
            lock (gate) open = Data.Items.Count(i => i.Period == period && !IsDone(i, now));
            if (open == 0 || !reminded.Add($"{period}:{next:O}")) continue;
            var label = period == ChecklistPeriod.Daily ? "Täglicher" : "Wöchentlicher";
            Reminder?.Invoke($"{label} Reset um {next:HH:mm}", $"{open} Punkt(e) noch offen");
        }
    }

    public void Save()
    {
        lock (gate) File.WriteAllText(file, JsonSerializer.Serialize(Data, Json));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
