using System.Collections.ObjectModel;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>Eine Zeile unter „Als Nächstes“: Termin, Checkliste, Feldboss.</summary>
public sealed record NextRow(string Icon, string Title, string Detail, string Right, bool Soon);

/// <summary>Ein Boss im Lauf: ab wann, wie lange, Gruppen-DPS, gelegt oder nicht.</summary>
public sealed record BossLine(string Name, string From, string Duration, string GroupDps, bool Killed);

/// <summary>
/// Ort „Jetzt“ im neuen Hauptfenster (docs/DESIGN.md): richtet sich nach der Lage. Nach einem Lauf
/// zuerst das Urteil (Bestzeit? mehr DPS als sonst?), dann die Messlatten, Gruppe und Bosse; ohne Lauf
/// ein leerer Zustand mit Vorschau und Startknopf. Rechts immer, was als Nächstes ansteht.
/// </summary>
public sealed partial class NowViewModel : ObservableObject
{
    private readonly RunArchive archive;
    private readonly TrackingMode mode;
    private readonly TimerService timers;
    private readonly ChecklistService checklist;
    private readonly FieldBosses fieldBosses;
    private readonly MeterSettings settings;

    public ObservableCollection<PlayerRow> Players { get; } = [];
    public ObservableCollection<BossLine> Bosses { get; } = [];
    public ObservableCollection<NextRow> Next { get; } = [];

    [ObservableProperty] private bool hasResult;
    [ObservableProperty] private string runName = "";
    [ObservableProperty] private string verdict = "";
    [ObservableProperty] private string runLine = "";

    [ObservableProperty] private IReadOnlyList<double> dpsHistory = [];
    [ObservableProperty] private double dpsCurrent = double.NaN;
    [ObservableProperty] private double dpsAverage = double.NaN;
    [ObservableProperty] private string dpsNeedle = "";
    [ObservableProperty] private string dpsAverageText = "";
    [ObservableProperty] private double dpsBest = double.NaN;
    [ObservableProperty] private string dpsDelta = "";
    [ObservableProperty] private bool dpsDeltaGood;

    [ObservableProperty] private IReadOnlyList<double> timeHistory = [];
    [ObservableProperty] private double timeCurrent = double.NaN;
    [ObservableProperty] private double timeAverage = double.NaN;
    [ObservableProperty] private string timeNeedle = "";
    [ObservableProperty] private string timeAverageText = "";
    [ObservableProperty] private double timeBestValue = double.NaN;
    [ObservableProperty] private string timeDelta = "";
    [ObservableProperty] private bool timeDeltaGood;
    [ObservableProperty] private bool timeBest;
    [ObservableProperty] private bool hasTimeHistory;

    [ObservableProperty] private bool hasHistory;
    [ObservableProperty] private string historyLegend = "";
    [ObservableProperty] private string firstRunText = "";

    [ObservableProperty] private bool isActive;
    [ObservableProperty] private string meterState = "";
    [ObservableProperty] private string meterButton = "";
    [ObservableProperty] private string meterAction = "";
    [ObservableProperty] private string hotkey = "";

    /// <summary>Beispielwerte für die Vorschau im leeren Zustand (ohne Zahlen beschriftet).</summary>
    public IReadOnlyList<double> PreviewHistory { get; } = [86, 90, 93, 92];

    public NowViewModel(RunArchive archive, TrackingMode mode, TimerService timers, ChecklistService checklist,
        FieldBosses fieldBosses, MeterSettings settings)
    {
        this.archive = archive;
        this.mode = mode;
        this.timers = timers;
        this.checklist = checklist;
        this.fieldBosses = fieldBosses;
        this.settings = settings;
    }

    public void Refresh()
    {
        IsActive = mode.IsCapturing;
        MeterState = Loc.T("shell.meter.on");
        MeterAction = Loc.T(IsActive ? "shell.meter.stop_short" : "shell.meter.start_short");
        MeterButton = IsActive ? MeterAction : Loc.T("shell.meter.start");
        Hotkey = settings.HotkeyText;
        ShowResult(archive.All().FirstOrDefault());
        ShowNext(DateTime.Now);
    }

    [RelayCommand]
    private void ToggleMeter() => mode.Toggle();

    private void ShowResult(ArchivedRun? run)
    {
        HasResult = run is not null;
        Players.Clear();
        Bosses.Clear();
        if (run is null) return;

        RunName = run.Name;
        var bosses = run.Bosses.Where(b => b.IsSignificant(run.TotalDamage)).ToList();
        int killed = bosses.Count(b => b.Killed);
        foreach (var b in bosses)
            Bosses.Add(new BossLine(b.Name, Loc.T("now.boss_from", RunArchive.Fmt(b.StartOffset)), Dur(b.Duration),
                Format.CompactFixed(b.Damage / Math.Max(1, b.Duration.TotalSeconds)) + "/s", b.Killed));

        // Messlatten: du gegen deine früheren Läufe mit demselben Namen
        var earlier = archive.All().Where(r => r.Name == run.Name && r.Id != run.Id && r.StartedAt < run.StartedAt).ToList();
        var user = run.Players.FirstOrDefault(p => p.IsUser);
        DpsHistory = earlier.Select(r => r.Players.FirstOrDefault(p => p.IsUser)?.Dps).OfType<double>().ToList();
        HasHistory = DpsHistory.Count > 0 && user is not null;
        FirstRunText = Loc.T("now.first_run", run.Name);
        HistoryLegend = Loc.T("now.scale_legend", DpsHistory.Count);
        DpsCurrent = user?.Dps ?? double.NaN;
        DpsAverage = DpsHistory.Count > 0 ? DpsHistory.Average() : double.NaN;
        DpsNeedle = user is null ? "" : Loc.T("now.scale_today", Format.CompactFixed(user.Dps) + "/s");
        DpsAverageText = double.IsFinite(DpsAverage) ? Loc.T("now.scale_avg", Format.CompactFixed(DpsAverage) + "/s") : "";
        DpsBest = DpsHistory.Count > 0 ? DpsHistory.Max() : double.NaN;
        if (user is not null && double.IsFinite(DpsAverage) && DpsAverage > 0)
        {
            double diff = (user.Dps / DpsAverage - 1) * 100;
            DpsDeltaGood = diff >= 0;
            DpsDelta = (diff >= 0 ? "+" : "−") + Math.Abs(diff).ToString("0", Loc.Culture) + " %";
        }

        var complete = earlier.Where(r => r.BossesKilled > 0 && r.BossesKilled == r.Bosses.Count).ToList();
        TimeHistory = complete.Select(r => r.DurationSeconds).ToList();
        HasTimeHistory = TimeHistory.Count > 0;
        TimeCurrent = run.DurationSeconds;
        TimeAverage = TimeHistory.Count > 0 ? TimeHistory.Average() : double.NaN;
        TimeNeedle = Loc.T("now.scale_today", Dur(run.Duration));
        TimeAverageText = double.IsFinite(TimeAverage) ? Loc.T("now.scale_avg", Dur(TimeSpan.FromSeconds(TimeAverage))) : "";
        TimeBestValue = TimeHistory.Count > 0 ? TimeHistory.Min() : double.NaN;
        if (double.IsFinite(TimeAverage))
        {
            double diff = run.DurationSeconds - TimeAverage;
            TimeDeltaGood = diff <= 0;
            TimeDelta = (diff <= 0 ? "−" : "+") + RunArchive.Fmt(TimeSpan.FromSeconds(Math.Abs(Math.Round(diff))));
        }
        double? best = complete.Count > 0 ? complete.Min(r => r.DurationSeconds) : null;
        TimeBest = best is double b0 && killed == bosses.Count && run.DurationSeconds < b0;

        // Urteil: das, was man nach einem Lauf in fünf Sekunden wissen will
        var parts = new List<string>();
        if (best is double b1)
            parts.Add(TimeBest ? Loc.T("now.verdict_best")
                : Loc.T("now.verdict_behind", Dur(TimeSpan.FromSeconds(run.DurationSeconds - b1))));
        if (user is not null && double.IsFinite(DpsAverage) && DpsAverage > 0)
        {
            double diff = (user.Dps / DpsAverage - 1) * 100;
            parts.Add(Loc.T(diff >= 0 ? "now.verdict_dps_up" : "now.verdict_dps_down", Math.Abs(diff).ToString("0", Loc.Culture)));
        }
        Verdict = parts.Count > 0 ? string.Join(Loc.T("now.verdict_join"), parts) : Loc.T("now.verdict_first");

        var bossText = bosses.Count == 0 ? ""
            : killed == bosses.Count ? (bosses.Count == 1 ? Loc.T("now.boss_one") : Loc.T("now.bosses_all", bosses.Count))
            : Loc.T("now.bosses_some", killed, bosses.Count);
        var ranked = run.Players.OrderByDescending(p => p.Damage).ToList();
        int rank = user is null ? 0 : ranked.IndexOf(user) + 1;
        var rankText = rank > 0 && ranked.Count > 1 ? Loc.T("now.rank", rank, ranked.Count) : "";
        RunLine = string.Join(" ", new[] { Loc.T("now.run_line", When(run.StartedAt + run.Duration)), bossText, rankText }.Where(s => s.Length > 0));

        long top = ranked.Count > 0 ? ranked[0].Damage : 0;
        foreach (var p in ranked)
        {
            Players.Add(new PlayerRow(p.PlayerId)
            {
                Name = p.Name.StartsWith("Player_", StringComparison.Ordinal) ? Loc.T("player.unknown", p.PlayerId) : p.Name,
                ClassName = GameText.ClassName(p.ClassId, ""),
                ClassBrush = ClassVisuals.Brush(p.ClassId),
                ClassIcon = ClassVisuals.Icon(p.ClassId),
                Dps = Format.CompactFixed(p.Dps) + "/s",
                Total = Format.CompactFixed(p.Damage),
                Share = Format.Percent(p.Share),
                BarPercent = top > 0 ? p.Damage * 100.0 / top : 0,
                IsUser = p.IsUser,
            });
        }
    }

    private void ShowNext(DateTime now)
    {
        Next.Clear();
        foreach (var t in timers.Items(now).Where(i => i.DueAt > now).OrderBy(i => i.DueAt).Take(2))
        {
            var left = t.DueAt!.Value - now;
            Next.Add(new NextRow("hourglass", t.Name, Loc.T("now.next_at", t.DueAt.Value.ToString("HH:mm")), Left(left), left.TotalMinutes <= 15));
        }

        foreach (var (period, key) in new[] { (ChecklistPeriod.Daily, "now.next_daily"), (ChecklistPeriod.Weekly, "now.next_weekly") })
        {
            var items = checklist.Data.Items.Where(i => i.Period == period).ToList();
            if (items.Count == 0) continue;
            int done = items.Count(i => checklist.IsDone(i, now));
            var reset = checklist.NextReset(period, now);
            Next.Add(new NextRow("checklist", Loc.T(key), Loc.T("now.next_reset", Left(reset - now)), Loc.T("now.next_done", done, items.Count), false));
        }

        var alive = fieldBosses.Views(now).Where(v => v.Favorite && v.Status == FieldBossStatus.Alive).Take(2);
        foreach (var v in alive)
            Next.Add(new NextRow("dragon", v.Boss.Name, v.Boss.Map.Name, Loc.T("now.next_alive"), true));
    }

    private static string Left(TimeSpan left) =>
        left.TotalHours >= 24 ? Loc.T("time.days_hours", (int)left.TotalDays, left.Hours)
        : left.TotalHours >= 1 ? Loc.T("time.hours_minutes", (int)left.TotalHours, left.Minutes)
        : Loc.T("now.minutes", Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)));

    /// <summary>Dauer mit Einheit, damit „18:18“ nicht wie eine Uhrzeit aussieht.</summary>
    private static string Dur(TimeSpan t) => Loc.T(t.TotalHours >= 1 ? "now.dur_hours" : "now.dur_minutes", RunArchive.Fmt(t));

    private static string When(DateTime at)
    {
        var day = at.Date == DateTime.Today ? Loc.T("now.today") : at.Date == DateTime.Today.AddDays(-1) ? Loc.T("now.yesterday") : at.ToString("d", Loc.Culture);
        return Loc.T("now.when", day, at.ToString("HH:mm"));
    }
}
