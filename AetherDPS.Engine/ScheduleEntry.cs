using System.Globalization;

namespace AetherDPS.Engine;

/// <summary>
/// Fester Zeitplan (Riss, Weltboss, Event …): entweder Wochentage + Uhrzeiten oder ein Takt
/// („alle 3 Stunden ab 02:00“). Alle Zeiten in deiner lokalen Uhrzeit.
/// </summary>
public sealed class ScheduleEntry
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Event";
    public bool Enabled { get; set; } = true;
    public bool ShowInOverlay { get; set; } = true;
    public bool Reminder { get; set; } = true;

    /// <summary>Leer = jeden Tag.</summary>
    public List<DayOfWeek> Days { get; set; } = [];

    /// <summary>Uhrzeiten „HH:mm“. Bei Takt: die erste Zeit ist der Startpunkt.</summary>
    public List<string> Times { get; set; } = [];

    /// <summary>&gt; 0: wiederholt sich in diesem Takt (Stunden) ab der ersten Uhrzeit.</summary>
    public double IntervalHours { get; set; }

    /// <summary>Vorlage, deren Zeiten für EU noch nicht bestätigt sind.</summary>
    public bool Unconfirmed { get; set; }

    public DateTime? Next(DateTime now)
    {
        var times = Times.Select(t => TimeSpan.TryParse(t, CultureInfo.InvariantCulture, out var ts) ? ts : (TimeSpan?)null)
            .OfType<TimeSpan>().OrderBy(t => t).ToList();
        if (times.Count == 0) return null;

        if (IntervalHours > 0)
            return TimerService.NextRift(now, times[0].ToString(@"hh\:mm"), IntervalHours);

        for (int d = 0; d <= 7; d++)
        {
            var date = now.Date.AddDays(d);
            if (Days.Count > 0 && !Days.Contains(date.DayOfWeek)) continue;
            foreach (var t in times)
                if (date + t > now) return date + t;
        }
        return null;
    }

    private static readonly string[] Short = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];

    /// <summary>„Mo, Do, Sa · 20:00, 23:00“ oder „alle 3 Std. ab 02:00“.</summary>
    public string Describe()
    {
        if (IntervalHours > 0)
            return $"alle {IntervalHours.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"))} Std. ab {Times.FirstOrDefault() ?? "–"}";
        var days = Days.Count == 0 ? "täglich" : string.Join(", ", Days.OrderBy(d => ((int)d + 6) % 7).Select(d => Short[(int)d]));
        return $"{days} · {string.Join(", ", Times)}";
    }

    public static List<ScheduleEntry> Defaults(string riftAnchor, double riftInterval, bool riftReminder) =>
    [
        new()
        {
            Name = "Raumzeit-Riss", Category = "Riss", Times = [riftAnchor], IntervalHours = riftInterval,
            Reminder = riftReminder, Unconfirmed = true,
        },
        new()
        {
            Name = "Riss-Eroberung (PvP)", Category = "Riss", Enabled = false, Unconfirmed = true,
            Days = [DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Saturday], Times = ["20:00", "23:00"],
        },
        new()
        {
            Name = "Abyss-Boss-Event", Category = "Weltboss", Enabled = false, Unconfirmed = true,
            Days = [DayOfWeek.Wednesday, DayOfWeek.Saturday], Times = ["21:45", "22:15", "22:45"],
        },
    ];
}
