using System.Collections.ObjectModel;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>Ein Lauf in der Liste; <see cref="DayHeader"/> steht über dem ersten Lauf eines Tages.</summary>
public sealed partial class RunListItem(string id, string title, bool isBest, bool isLive,
    string? dayHeader = null, string time = "", string duration = "", string yourDps = "", double dpsPercent = double.NaN) : ObservableObject
{
    public string Id { get; } = id;
    public string Title { get; } = title;
    public bool IsBest { get; } = isBest;
    public bool IsLive { get; } = isLive;
    public string? DayHeader { get; } = dayHeader;
    public string Time { get; } = time;
    public string Duration { get; } = duration;
    public string YourDps { get; } = yourDps;
    /// <summary>Deine DPS im Vergleich zu deinem besten Lauf desselben Namens (0…100), für die Mini-Skala.</summary>
    public double DpsPercent { get; } = dpsPercent;
    public bool HasDpsPercent => double.IsFinite(DpsPercent);
    public bool HasDayHeader => DayHeader is not null;

    [ObservableProperty] private bool isSelected;
}

/// <summary>Skill in der Kampfanalyse: Spalten statt eines zusammengeklebten Textes, mit offiziellem Bild.</summary>
public sealed partial class SkillLine : WithIcon
{
    public SkillLine(string name, string damage, string share, string hits, string crit, double barPercent)
    {
        (Name, Damage, Share, Hits, Crit, BarPercent) = (name, damage, share, hits, crit, barPercent);
        LoadIcon(SkillIcons.ForName(name));
    }

    public string Name { get; }
    public string Damage { get; }
    public string Share { get; }
    public string Hits { get; }
    public string Crit { get; }
    public double BarPercent { get; }
}

/// <summary>Buff in der Kampfanalyse: Anteil der Kampfzeit und wie oft; Bild vom gleichnamigen Skill, sonst keins.</summary>
public sealed partial class BuffLine : WithIcon
{
    public BuffLine(string name, string uptime, string count, double barPercent)
    {
        (Name, Uptime, Count, BarPercent) = (name, uptime, count, barPercent);
        LoadIcon(SkillIcons.ForName(name));
    }

    public string Name { get; }
    public string Uptime { get; }
    public string Count { get; }
    public double BarPercent { get; }
}

/// <summary>Ort „Kämpfe“: alle gespeicherten Läufe, Details, Kampfanalyse je Spieler, Teilen, Löschen mit Rückgängig.</summary>
public sealed partial class RunsViewModel : ObservableObject
{
    private const string LiveId = "live";
    private readonly RunArchive archive;
    private readonly RunTracker tracker;
    private readonly TrackingMode mode;
    private readonly MeterSettings meterSettings;
    private ArchivedRun? current;
    private ArchivedRun? deleted;
    private DispatcherTimer? undoTimer;

    public ObservableCollection<RunListItem> Runs { get; } = [];
    public ObservableCollection<PlayerRow> Players { get; } = [];
    public ObservableCollection<BossLine> Bosses { get; } = [];
    public ObservableCollection<SkillLine> Skills { get; } = [];
    public ObservableCollection<BuffLine> Buffs { get; } = [];

    [ObservableProperty] private RunListItem? selectedRun;
    [ObservableProperty] private PlayerRow? selectedPlayer;
    [ObservableProperty] private bool hasRun;
    [ObservableProperty] private bool hasAnyRun;
    [ObservableProperty] private bool isLive;
    [ObservableProperty] private string runTitle = "";
    [ObservableProperty] private string verdict = "";
    [ObservableProperty] private bool isBestTime;
    [ObservableProperty] private string runSummary = "";
    [ObservableProperty] private bool hasPlayer;
    [ObservableProperty] private string playerTitle = "";
    [ObservableProperty] private string playerFacts = "";
    [ObservableProperty] private string notice = "";
    [ObservableProperty] private bool canUndo;
    [ObservableProperty] private bool hasBuffs;

    /// <summary>Die View rendert die Details als Bild (Control nötig) – Anfrage per Event.</summary>
    public event EventHandler? CopyImageRequested;

    public RunsViewModel(RunArchive archive, RunTracker tracker, TrackingMode mode, MeterSettings meterSettings)
    {
        this.meterSettings = meterSettings;
        meterSettings.Changed += (_, _) => Dispatcher.UIThread.Post(RefreshWebhook);
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
        if (mode.IsActive) Runs.Add(new RunListItem(LiveId, Loc.T("fights.live"), false, true));
        string? lastDay = null;
        var bestDps = archive.All().GroupBy(r => r.Name)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Players.FirstOrDefault(p => p.IsUser)?.Dps ?? 0).DefaultIfEmpty(0).Max());
        foreach (var run in archive.All())
        {
            var best = archive.BestOf(run.Name);
            var user = run.Players.FirstOrDefault(p => p.IsUser);
            var day = RunComparison.Day(run.StartedAt);
            Runs.Add(new RunListItem(run.Id, run.Name, best?.Id == run.Id && run.BossesKilled > 0, false,
                day != lastDay ? day : null, run.StartedAt.ToString("HH:mm"), RunComparison.Dur(run.Duration),
                user is null ? "" : Format.CompactFixed(user.Dps) + "/s",
                user is not null && bestDps[run.Name] > 0 ? user.Dps * 100 / bestDps[run.Name] : double.NaN));
            lastDay = day;
        }
        HasAnyRun = Runs.Count > 0;
        SelectedRun = Runs.FirstOrDefault(r => r.Id == keep) ?? Runs.FirstOrDefault();
    }

    partial void OnSelectedRunChanged(RunListItem? value)
    {
        foreach (var r in Runs) r.IsSelected = ReferenceEquals(r, value);
        ShowRun();
    }

    [RelayCommand]
    private void SelectRun(RunListItem? item)
    {
        if (item is not null) SelectedRun = item;
    }

    [RelayCommand]
    private void SelectPlayer(PlayerRow? row)
    {
        if (row is not null) SelectedPlayer = row;
    }

    partial void OnSelectedPlayerChanged(PlayerRow? value)
    {
        foreach (var p in Players) p.IsSelected = ReferenceEquals(p, value);
        ShowPlayer();
    }

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

        IsLive = SelectedRun!.IsLive;
        var cmp = new RunComparison(current, archive.All());
        RunTitle = IsLive ? Loc.T("fights.live") : current.Name;
        Verdict = IsLive ? "" : cmp.Verdict;
        IsBestTime = !IsLive && cmp.IsBestTime;
        var combat = RunComparison.Dur(TimeSpan.FromSeconds(current.CombatSeconds));
        RunSummary = string.Join(" ", new[]
        {
            IsLive ? "" : Loc.T("now.run_line", RunComparison.When(current.StartedAt + current.Duration)),
            Loc.T("fights.summary", RunComparison.Dur(current.Duration), combat, Format.CompactFixed(current.TotalDamage), Format.CompactFixed(current.PartyDps) + "/s"),
            cmp.BossText,
        }.Where(s => s.Length > 0));

        var selectedId = SelectedPlayer?.PlayerId;
        Players.Clear();
        foreach (var row in PlayerRows.From(current)) Players.Add(row);
        Bosses.Clear();
        foreach (var b in RunComparison.BossLines(current)) Bosses.Add(b);

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

        PlayerTitle = SelectedPlayer!.Name;
        var facts = new List<string>
        {
            Loc.T("runs.active", p.ActivityPercent.ToString("0", Loc.Culture)),
            p.Deaths == 0 ? Loc.T("runs.no_deaths") : Loc.T("runs.deaths", p.Deaths),
            Loc.T("runs.hits", p.Skills.Sum(s => s.Hits)),
            p.DamageTaken > 0 ? Loc.T("runs.taken", Format.CompactFixed(p.DamageTaken)) : "",
        };
        PlayerFacts = string.Join(Loc.T("now.verdict_join"), facts.Where(f => f.Length > 0));

        long top = p.Skills.Count > 0 ? p.Skills.Max(s => s.Damage) : 0;
        foreach (var s in p.Skills)
            Skills.Add(new SkillLine(s.Name, Format.CompactFixed(s.Damage), Format.Percent(s.Share), s.Hits + "×",
                s.CritRate.ToString("0", Loc.Culture) + " %", top > 0 ? s.Damage * 100.0 / top : 0));
        foreach (var b in p.Buffs)
            Buffs.Add(new BuffLine(b.Name, b.UptimePercent.ToString("0", Loc.Culture) + " %", b.Applications + "×", b.UptimePercent));
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
        ShowNotice(Loc.T("runs.copied_text"));
    }

    [RelayCommand]
    private void CopyImage() => CopyImageRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Vom View gesetzt: rendert die Lauf-Karte als PNG.</summary>
    public Func<byte[]>? RenderImage { get; set; }

    public bool HasWebhook => AetherDPS.Engine.Community.DiscordWebhook.IsValid(meterSettings.DiscordWebhook);

    [ObservableProperty] private bool isPosting;

    /// <summary>Lauf als Bild + Text in den eingetragenen Discord-Kanal.</summary>
    [RelayCommand]
    private async Task SendToDiscordAsync()
    {
        if (current is null || IsPosting || !HasWebhook) return;
        IsPosting = true;
        byte[]? png = null;
        try { png = RenderImage?.Invoke(); } catch (Exception) { /* dann nur Text */ }
        var ok = await AetherDPS.Engine.Community.DiscordWebhook.SendAsync(meterSettings.DiscordWebhook!,
            RunArchive.ToDiscordText(current, GameText.ClassName), png);
        IsPosting = false;
        ShowNotice(Loc.T(ok ? "runs.discord_sent" : "runs.discord_failed"));
    }

    public void RefreshWebhook() => OnPropertyChanged(nameof(HasWebhook));

    public void ReportImageCopied(bool ok) => ShowNotice(ok ? Loc.T("runs.copied_image") : Loc.T("runs.copy_failed"));

    /// <summary>Löscht sofort; „Rückgängig“ holt den Lauf ein paar Sekunden lang zurück (statt einer Nachfrage).</summary>
    [RelayCommand]
    private void DeleteRun()
    {
        if (current is null || SelectedRun?.IsLive != false) return;
        deleted = current;
        archive.Delete(current.Id);
        ShowNotice(Loc.T("fights.deleted", deleted.Name), undo: true);
    }

    [RelayCommand]
    private void Undo()
    {
        if (deleted is null) return;
        var run = deleted;
        deleted = null;
        archive.Restore(run);
        SelectedRun = Runs.FirstOrDefault(r => r.Id == run.Id) ?? SelectedRun;
        ShowNotice(Loc.T("fights.restored"));
    }

    private void ShowNotice(string text, bool undo = false)
    {
        Notice = text;
        CanUndo = undo;
        undoTimer?.Stop();
        undoTimer = new DispatcherTimer(TimeSpan.FromSeconds(undo ? 8 : 5), DispatcherPriority.Background, (_, _) =>
        {
            undoTimer?.Stop();
            Notice = "";
            CanUndo = false;
            deleted = null;
        });
        undoTimer.Start();
    }
}
