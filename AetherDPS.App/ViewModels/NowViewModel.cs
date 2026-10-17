using System.Collections.ObjectModel;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>Eine Zeile unter „Als Nächstes“: Termin, Checkliste, Feldboss.</summary>
public sealed record NextRow(string Icon, string Title, string Detail, string Right, bool Soon);

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
    [ObservableProperty] private string runKind = "";
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

    /// <summary>Karte „Du“ rechts: dein Charakter und sein Verlauf.</summary>
    public SelfProgress Progress { get; }

    public NowViewModel(RunArchive archive, TrackingMode mode, TimerService timers, ChecklistService checklist,
        FieldBosses fieldBosses, MeterSettings settings, SelfProgress progress)
    {
        Progress = progress;
        this.archive = archive;
        this.mode = mode;
        this.timers = timers;
        this.checklist = checklist;
        this.fieldBosses = fieldBosses;
        this.settings = settings;
    }

    private string? shownRun;
    private DateTime nextShownAt;

    /// <summary>Jede Sekunde vom Hauptfenster. Ergebnis nur neu, wenn ein neuer Lauf da ist; „Als Nächstes“ alle 15 s.</summary>
    public void Refresh(bool full = true)
    {
        IsActive = mode.IsCapturing;
        MeterState = Loc.T("shell.meter.on");
        MeterAction = Loc.T(IsActive ? "shell.meter.stop_short" : "shell.meter.start_short");
        MeterButton = IsActive ? MeterAction : Loc.T("shell.meter.start");
        Hotkey = settings.HotkeyText;
        if (!full) return;
        var latest = archive.All().FirstOrDefault();
        var key = latest is null ? "" : latest.Id + ":" + archive.All().Count;
        if (key != shownRun)
        {
            shownRun = key;
            ShowResult(latest);
        }
        if (DateTime.Now >= nextShownAt)
        {
            nextShownAt = DateTime.Now.AddSeconds(15);
            ShowNext(DateTime.Now);
        }
    }

    [RelayCommand]
    private void ToggleMeter() => mode.Toggle();

    private void ShowResult(ArchivedRun? run)
    {
        HasResult = run is not null;
        Players.Clear();
        Bosses.Clear();
        if (run is null) return;

        var cmp = new RunComparison(run, archive.All());
        RunName = run.Name;
        RunKind = cmp.KindText;
        Verdict = cmp.Verdict;
        TimeBest = cmp.IsBestTime;
        RunLine = string.Join(" ", new[] { Loc.T("now.run_line", RunComparison.When(run.StartedAt + run.Duration)), cmp.BossText, cmp.RankText }
            .Where(s => s.Length > 0));
        foreach (var b in RunComparison.BossLines(run)) Bosses.Add(b);

        // Messlatten: du gegen deine früheren Läufe mit demselben Namen
        var user = cmp.User;
        HasHistory = cmp.HasScale;
        // Ohne Skala ein ehrlicher Satz statt verwirrender Striche: kein Vergleich, erster oder zweiter Lauf
        FirstRunText = !cmp.Comparable ? Loc.T("now.not_comparable_text", cmp.KindText)
            : cmp.DpsHistory.Count == 1 ? Loc.T("now.one_earlier", cmp.KindText, Format.CompactFixed(cmp.DpsHistory[0]) + "/s")
            : Loc.T("now.first_run_in", cmp.KindText);
        HistoryLegend = Loc.T("now.scale_legend_in", cmp.KindText, cmp.DpsHistory.Count);
        DpsHistory = cmp.DpsHistory;
        DpsAverage = cmp.DpsAverage;
        DpsBest = cmp.DpsBest;
        DpsCurrent = user?.Dps ?? double.NaN;
        DpsNeedle = user is null ? "" : Loc.T("now.scale_today", Format.CompactFixed(user.Dps) + "/s");
        DpsAverageText = double.IsFinite(DpsAverage) ? Loc.T("now.scale_avg", Format.CompactFixed(DpsAverage) + "/s") : "";
        if (user is not null && double.IsFinite(DpsAverage) && DpsAverage > 0)
        {
            double diff = (user.Dps / DpsAverage - 1) * 100;
            DpsDeltaGood = diff >= 0;
            DpsDelta = (diff >= 0 ? "+" : "−") + Math.Abs(diff).ToString("0", Loc.Culture) + " %";
        }

        TimeHistory = cmp.TimeHistory;
        HasTimeHistory = cmp.HasScale && TimeHistory.Count >= 2;
        TimeCurrent = run.DurationSeconds;
        TimeAverage = cmp.TimeAverage;
        TimeBestValue = cmp.TimeBest;
        TimeNeedle = Loc.T("now.scale_today", RunComparison.Dur(run.Duration));
        TimeAverageText = double.IsFinite(TimeAverage) ? Loc.T("now.scale_avg", RunComparison.Dur(TimeSpan.FromSeconds(TimeAverage))) : "";
        if (double.IsFinite(TimeAverage))
        {
            double diff = run.DurationSeconds - TimeAverage;
            TimeDeltaGood = diff <= 0;
            TimeDelta = (diff <= 0 ? "−" : "+") + RunArchive.Fmt(TimeSpan.FromSeconds(Math.Abs(Math.Round(diff))));
        }

        foreach (var row in PlayerRows.From(run)) Players.Add(row);
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
}
