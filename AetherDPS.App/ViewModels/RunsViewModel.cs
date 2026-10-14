using System.Collections.ObjectModel;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

public sealed record RunListItem(string Id, string Title, string Subtitle, bool IsBest, bool IsLive);

public sealed record BossRow(string Name, string Detail, bool Killed);

public sealed record AnalysisRow(string Name, string Value, double BarPercent);

/// <summary>Reiter „Läufe“: Archiv, Details, Kampfanalyse, Teilen.</summary>
public sealed partial class RunsViewModel : ObservableObject
{
    private const string LiveId = "live";
    private readonly RunArchive archive;
    private readonly RunTracker tracker;
    private readonly TrackingMode mode;
    private ArchivedRun? current;

    public ObservableCollection<RunListItem> Runs { get; } = [];
    public ObservableCollection<PlayerRow> Players { get; } = [];
    public ObservableCollection<BossRow> Bosses { get; } = [];
    public ObservableCollection<AnalysisRow> Skills { get; } = [];
    public ObservableCollection<AnalysisRow> Buffs { get; } = [];

    [ObservableProperty] private RunListItem? selectedRun;
    [ObservableProperty] private PlayerRow? selectedPlayer;
    [ObservableProperty] private bool hasRun;
    [ObservableProperty] private string runTitle = "";
    [ObservableProperty] private string runSummary = "";
    [ObservableProperty] private string comparison = "";
    [ObservableProperty] private bool hasPlayer;
    [ObservableProperty] private string playerTitle = "";
    [ObservableProperty] private string playerFacts = "";
    [ObservableProperty] private string notice = "";
    [ObservableProperty] private bool hasBuffs;

    /// <summary>Die View rendert die Details als Bild (Control nötig) – Anfrage per Event.</summary>
    public event EventHandler? CopyImageRequested;

    public RunsViewModel(RunArchive archive, RunTracker tracker, TrackingMode mode)
    {
        this.archive = archive;
        this.tracker = tracker;
        this.mode = mode;
        archive.Changed += (_, _) => Dispatcher.UIThread.Post(ReloadList);
        mode.Changed += (_, _) => Dispatcher.UIThread.Post(ReloadList);
        ReloadList();
    }

    public void ReloadList()
    {
        var keep = SelectedRun?.Id;
        Runs.Clear();
        if (mode.IsActive) Runs.Add(new RunListItem(LiveId, "● Laufender Lauf", "aktualisiert sich live", false, true));
        foreach (var run in archive.All())
        {
            var best = archive.BestOf(run.Name);
            var user = run.Players.FirstOrDefault(p => p.IsUser);
            var subtitle = $"{run.StartedAt:dd.MM. HH:mm} · {RunArchive.Fmt(run.Duration)} · Gruppe {Format.Compact(run.PartyDps)}/s"
                           + (user is null ? "" : $" · du {Format.Compact(user.Dps)}/s");
            Runs.Add(new RunListItem(run.Id, run.Name, subtitle, best?.Id == run.Id && run.BossesKilled > 0, false));
        }
        SelectedRun = Runs.FirstOrDefault(r => r.Id == keep) ?? Runs.FirstOrDefault();
    }

    partial void OnSelectedRunChanged(RunListItem? value) => ShowRun();

    partial void OnSelectedPlayerChanged(PlayerRow? value) => ShowPlayer();

    /// <summary>Jede Sekunde vom Hauptfenster – nur der laufende Lauf ändert sich.</summary>
    public void Tick()
    {
        if (SelectedRun?.IsLive == true) ShowRun();
    }

    private void ShowRun()
    {
        current = SelectedRun switch
        {
            null => null,
            { IsLive: true } => archive.Build(tracker.Snapshot()),
            var item => archive.All().FirstOrDefault(r => r.Id == item.Id),
        };
        HasRun = current is not null;
        if (current is null) return;

        RunTitle = SelectedRun!.IsLive ? "Laufender Lauf" : current.Name;
        RunSummary = $"{current.StartedAt:dd.MM.yyyy HH:mm} · Dauer {RunArchive.Fmt(current.Duration)} · " +
                     $"Kampfzeit {RunArchive.Fmt(TimeSpan.FromSeconds(current.CombatSeconds))} · " +
                     $"Gesamt {Format.Compact(current.TotalDamage)} · Gruppe {Format.Compact(current.PartyDps)}/s";

        var best = archive.BestOf(current.Name);
        var user = current.Players.FirstOrDefault(p => p.IsUser);
        var avg = archive.AverageUserDps(current.Name, current.Id);
        var parts = new List<string>();
        if (best is not null && current.BossesKilled > 0)
            parts.Add(best.Id == current.Id ? "🏆 Bestzeit!" : $"Bestzeit {RunArchive.Fmt(best.Duration)} ({best.StartedAt:dd.MM.})");
        if (user is not null && avg is double a && a > 0)
            parts.Add($"Deine DPS {Format.Compact(user.Dps)}/s – {(user.Dps >= a ? "+" : "")}{(user.Dps / a - 1) * 100:0} % ggü. deinem Ø ({Format.Compact(a)}/s)");
        Comparison = string.Join(" · ", parts);

        long top = current.Players.Count > 0 ? current.Players.Max(p => p.Damage) : 0;
        var selectedId = SelectedPlayer?.PlayerId;
        Players.Clear();
        foreach (var p in current.Players)
        {
            Players.Add(new PlayerRow(p.PlayerId)
            {
                Name = p.Name.StartsWith("Player_", StringComparison.Ordinal) ? Loc.T("player.unknown", p.PlayerId) : p.Name,
                ClassName = GameText.ClassName(p.ClassId, ""),
                ClassBrush = ClassVisuals.Brush(p.ClassId),
                ClassIcon = ClassVisuals.Icon(p.ClassId),
                Dps = Format.Compact(p.Dps) + "/s",
                Total = Format.Compact(p.Damage),
                Share = Format.Percent(p.Share),
                BarPercent = top > 0 ? p.Damage * 100.0 / top : 0,
                IsUser = p.IsUser,
            });
        }

        Bosses.Clear();
        foreach (var b in current.Bosses.Where(b => b.IsSignificant(current.TotalDamage)))
            Bosses.Add(new BossRow(b.Name,
                Loc.T("runs.boss_detail", RunArchive.Fmt(b.StartOffset), RunArchive.Fmt(b.Duration), Format.Compact(b.Damage))
                    + (b.Attempts > 1 ? " · " + Loc.T("runs.attempts", b.Attempts) : ""),
                b.Killed));

        SelectedPlayer = Players.FirstOrDefault(p => p.PlayerId == selectedId)
                         ?? Players.FirstOrDefault(p => p.IsUser) ?? Players.FirstOrDefault();
        ShowPlayer();
    }

    private void ShowPlayer()
    {
        var p = current?.Players.FirstOrDefault(x => x.PlayerId == SelectedPlayer?.PlayerId);
        HasPlayer = p is not null;
        Skills.Clear();
        Buffs.Clear();
        if (p is null) return;

        PlayerTitle = $"Kampfanalyse: {SelectedPlayer!.Name}";
        var facts = new List<string>
        {
            $"Aktiv {p.ActivityPercent:0} % der Kampfzeit",
            p.Deaths == 0 ? "keine Tode" : $"{p.Deaths}× gestorben",
            $"{p.Skills.Sum(s => s.Hits)} Treffer",
        };
        PlayerFacts = string.Join(" · ", facts);

        long top = p.Skills.Count > 0 ? p.Skills.Max(s => s.Damage) : 0;
        foreach (var s in p.Skills)
            Skills.Add(new AnalysisRow(s.Name, $"{Format.Compact(s.Damage)} · {Format.Percent(s.Share)} · {s.Hits}× · Krit {s.CritRate:0} %",
                top > 0 ? s.Damage * 100.0 / top : 0));
        foreach (var b in p.Buffs)
            Buffs.Add(new AnalysisRow(b.Name, $"{b.UptimePercent:0} % aktiv · {b.Applications}×", b.UptimePercent));
        HasBuffs = Buffs.Count > 0;
    }

    [RelayCommand]
    private async Task CopyTextAsync()
    {
        if (current is null) return;
        var text = RunArchive.ToDiscordText(current, GameText.ClassName);
        var clipboard = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d
            ? d.MainWindow?.Clipboard : null;
        if (clipboard is null) return;
        await clipboard.SetTextAsync(text);
        Notice = "Text kopiert – in Discord mit STRG+V einfügen.";
    }

    [RelayCommand]
    private void CopyImage() => CopyImageRequested?.Invoke(this, EventArgs.Empty);

    public void ReportImageCopied(bool ok) =>
        Notice = ok ? "Bild kopiert – in Discord mit STRG+V einfügen." : "Bild konnte nicht kopiert werden.";

    [RelayCommand]
    private void DeleteRun()
    {
        if (current is null || SelectedRun?.IsLive != false) return;
        archive.Delete(current.Id);
    }
}
