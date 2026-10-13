using System.Collections.ObjectModel;
using System.Diagnostics;
using Aion2Meter.App.Services;
using Aion2Meter.Engine;
using Aion2Meter.Engine.Capture;
using AionDpsMeter.Services.Models;
using AionDpsMeter.Services.PacketCapture;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Services.Services.Settings;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Aion2Meter.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly CombatSessionManager sessions;
    private readonly IPacketService pipeline;
    private readonly IPacketCaptureDevice capture;
    private readonly RingRecorder recorder;
    private readonly TrackingMode mode;
    private readonly RunTracker run;
    private readonly PartyRoster roster;
    private readonly MeterSettings meterSettings;
    private readonly TimerService timers;
    private readonly IAppSettingsService settings;
    private readonly MeterOptions options;
    private readonly ILogger<MainViewModel> logger;
    private readonly DispatcherTimer timer;
    private readonly Dictionary<long, PlayerRow> rowsById = [];
    private string? startError;
    private int pingMs = -1;
    private DateTime noticeUntil;

    public ObservableCollection<PlayerRow> Players { get; } = [];
    public ObservableCollection<SkillRow> Skills { get; } = [];

    [ObservableProperty] private string targetName = Loc.T("target.none");
    [ObservableProperty] private double targetHpPercent;
    [ObservableProperty] private bool hasTarget;
    [ObservableProperty] private string duration = "00:00";
    [ObservableProperty] private string partyDps = "";
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private bool npcapMissing;
    [ObservableProperty] private bool showDetails;
    [ObservableProperty] private string detailsTitle = Loc.T("details.empty");
    [ObservableProperty] private bool hasPlayers;
    [ObservableProperty] private bool isClickThrough;
    [ObservableProperty] private string notice = "";
    [ObservableProperty] private bool isStandby = true;
    [ObservableProperty] private string standbyText = "";
    [ObservableProperty] private string riftText = "";
    [ObservableProperty] private bool showNameHint;
    [ObservableProperty] private bool showTargetView;
    [ObservableProperty] private string viewLabel = Loc.T("view.run");

    private long? selectedPlayerId;

    public MainViewModel(CombatSessionManager sessions, IPacketService pipeline, IPacketCaptureDevice capture,
        RingRecorder recorder, TrackingMode mode, RunTracker run, PartyRoster roster, MeterSettings meterSettings, TimerService timers,
        IAppSettingsService settings, MeterOptions options, ILogger<MainViewModel> logger)
    {
        this.sessions = sessions;
        this.pipeline = pipeline;
        this.capture = capture;
        this.recorder = recorder;
        this.mode = mode;
        this.run = run;
        this.roster = roster;
        this.meterSettings = meterSettings;
        this.timers = timers;
        StandbyText = Loc.T("standby.text", meterSettings.HotkeyText);
        meterSettings.Changed += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            StandbyText = Loc.T("standby.text", meterSettings.HotkeyText);
            Refresh();
        });
        mode.Changed += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            IsStandby = !mode.IsActive;
            if (mode.IsActive) { rowsById.Clear(); Players.Clear(); Skills.Clear(); }
            Refresh();
        });
        this.settings = settings;
        this.options = options;
        this.logger = logger;

        sessions.PingUpdated += (_, ms) => pingMs = ms;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => Refresh());
    }

    public void Start()
    {
        NpcapMissing = options.ReplayFile is null && !Npcap.IsInstalled();
        if (!NpcapMissing)
        {
            try
            {
                pipeline.Start();
            }
            catch (Exception ex)
            {
                startError = ex.Message;
                logger.LogError(ex, "Mitlesen konnte nicht starten");
            }
        }
        timer.Start();
        Refresh();
    }

    public void Stop()
    {
        timer.Stop();
        try { pipeline.Stop(); } catch (Exception ex) { logger.LogWarning(ex, "Stop"); }
    }

    private void Refresh()
    {
        UpdateStatus();
        RiftText = RiftCountdown(timers);

        var target = sessions.GetActiveTargetInfo();
        HasTarget = target is not null;
        TargetHpPercent = target is { HpTotal: > 0 } ? (double)target.HpCurrent / target.HpTotal * 100 : 0;
        string targetName = target is null ? Loc.T("target.none")
            : target.MobCode == 0 ? Loc.T("target.unknown") : GermanGameData.MobName(target);

        List<MeterLine> entries;
        if (ShowTargetView)
        {
            TargetName = targetName;
            Duration = sessions.GetCombatDuration().ToString(@"mm\:ss");
            PartyDps = Loc.T("footer.party", Format.Compact(sessions.GetPartyDps()));
            entries = sessions.PlayerStats
                .Select(p => new MeterLine(p.PlayerId, p.PlayerName, p.IsUser, p.IsIdentified, p.ClassId, p.ClassName,
                    p.TotalDamage, p.DamagePerSecond, p.DamagePercentage))
                .ToList();
        }
        else
        {
            var snapshot = run.Snapshot();
            TargetName = Loc.T("run.title", meterSettings.HotkeyText);
            Duration = FormatDuration(snapshot.Elapsed);
            PartyDps = Loc.T("footer.party", Format.Compact(snapshot.PartyDps));
            entries = snapshot.Players
                .Select(p => new MeterLine(p.PlayerId, p.Name, p.IsUser, p.IsIdentified, p.ClassId, p.ClassName,
                    p.Damage, p.Dps, p.Share))
                .ToList();
        }

        // Fremde ausblenden (Gruppenmodus) und namenlose Beschwörungen ihrem Besitzer zuschlagen.
        var merge = MeterFilter.Apply(entries, roster, meterSettings);
        entries = merge.Lines;
        mergedSummons = merge.MergedInto;
        PartyDps = Loc.T("footer.party", Format.Compact(entries.Sum(e => e.DamagePerSecond)));

        UpdatePlayers(entries);
        if (ShowDetails) UpdateSkills();
    }

    /// <summary>„Riss 1:23:45“ – Countdown bis zum nächsten Raumzeit-Riss.</summary>
    public static string RiftCountdown(TimerService timers)
    {
        var now = DateTime.Now;
        var next = TimerService.NextRift(now, timers.Settings.RiftAnchor, timers.Settings.RiftIntervalHours);
        var left = next - now;
        return Loc.T("rift.short", left.TotalHours >= 1 ? left.ToString(@"h\:mm\:ss") : left.ToString(@"mm\:ss"));
    }

    private static string FormatDuration(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");

    private Dictionary<long, List<long>> mergedSummons = [];

    private void UpdateStatus()
    {
        if (DateTime.Now < noticeUntil)
        {
            StatusText = Notice;
            return;
        }

        if (NpcapMissing)
        {
            IsConnected = false;
            StatusText = Loc.T("status.npcap_missing");
        }
        else if (startError is not null)
        {
            IsConnected = false;
            StatusText = Loc.T("status.error", startError);
        }
        else if (capture is ReplayCaptureDevice replay)
        {
            IsConnected = true;
            StatusText = Loc.T(replay.IsFinished ? "status.replay_done" : "status.replay");
        }
        else if (capture.DeviceName is null)
        {
            IsConnected = false;
            StatusText = Loc.T("status.searching");
        }
        else
        {
            IsConnected = true;
            StatusText = pingMs >= 0
                ? $"{Loc.T("status.connected")} · {Loc.T("status.ping", pingMs)}"
                : Loc.T("status.connected");
        }
    }

    private void UpdatePlayers(List<MeterLine> all)
    {
        var stats = all
            .Where(p => p.IsIdentified || p.DamagePercentage > 1)
            .OrderByDescending(p => p.TotalDamage)
            .ToList();
        long top = stats.Count > 0 ? stats[0].TotalDamage : 0;

        var seen = new HashSet<long>();
        foreach (var p in stats)
        {
            seen.Add(p.PlayerId);
            if (!rowsById.TryGetValue(p.PlayerId, out var row))
            {
                row = new PlayerRow(p.PlayerId);
                rowsById[p.PlayerId] = row;
            }

            row.IsUser = p.IsUser;
            row.Name = DisplayName(p);
            row.ClassName = GermanGameData.ClassName(p.ClassId, p.ClassName);
            row.ClassBrush = ClassVisuals.Brush(p.ClassId);
            row.ClassIcon = ClassVisuals.Icon(p.ClassId);
            row.Dps = Format.Compact(p.DamagePerSecond) + "/s";
            row.Total = Format.Compact(p.TotalDamage);
            row.Share = Format.Percent(p.DamagePercentage);
            row.BarPercent = top > 0 ? (double)p.TotalDamage / top * 100 : 0;
            row.SortKey = p.TotalDamage;
            row.IsSelected = selectedPlayerId == p.PlayerId;
        }

        foreach (var id in rowsById.Keys.Where(id => !seen.Contains(id)).ToList())
            rowsById.Remove(id);

        // Liste nur umbauen, wenn sich Reihenfolge oder Mitglieder ändern – vermeidet Flackern.
        var ordered = stats.Select(p => rowsById[p.PlayerId]).ToList();
        if (!ordered.SequenceEqual(Players))
        {
            Players.Clear();
            foreach (var row in ordered) Players.Add(row);
        }
        HasPlayers = Players.Count > 0;
        // Eigener Name kommt nur bei Login/Teleport – solange er fehlt, kurz erklären.
        ShowNameHint = HasPlayers && !stats.Any(p => p.IsUser);
    }

    private string DisplayName(MeterLine p)
    {
        if (p.IsUser && settings.IsNicknameHidden) return Loc.T("player.you");
        if (string.IsNullOrWhiteSpace(p.PlayerName) || p.PlayerName.StartsWith("Player_", StringComparison.Ordinal))
            return Loc.T("player.unknown", p.PlayerId);
        return p.PlayerName;
    }

    private void UpdateSkills()
    {
        if (selectedPlayerId is not long id || !rowsById.TryGetValue(id, out var row))
        {
            DetailsTitle = Loc.T("details.empty");
            if (Skills.Count > 0) Skills.Clear();
            return;
        }

        DetailsTitle = Loc.T("details.title", row.Name);
        var rows = ShowTargetView ? TargetSkills(id) : RunSkills((int)id);

        if (!rows.SequenceEqual(Skills))
        {
            Skills.Clear();
            foreach (var r in rows) Skills.Add(r);
        }
    }

    private List<SkillRow> TargetSkills(long id)
    {
        var skillStats = sessions.GetPlayerSkillStats(id).OrderByDescending(s => s.TotalDamage).Take(12).ToList();
        long top = skillStats.Count > 0 ? skillStats[0].TotalDamage : 0;
        return skillStats.Select(s => new SkillRow(
            s.SkillName, Format.Compact(s.TotalDamage), Format.Percent(s.DamagePercentage), s.HitCount,
            Format.Percent(s.CriticalRate), top > 0 ? (double)s.TotalDamage / top * 100 : 0)).ToList();
    }

    private List<SkillRow> RunSkills(int id)
    {
        var skillStats = run.Skills(id, mergedSummons.GetValueOrDefault(id)).Take(12).ToList();
        long top = skillStats.Count > 0 ? skillStats[0].Damage : 0;
        return skillStats.Select(s => new SkillRow(
            s.Name, Format.Compact(s.Damage), Format.Percent(s.Share), s.Hits,
            Format.Percent(s.CritRate), top > 0 ? (double)s.Damage / top * 100 : 0)).ToList();
    }

    [RelayCommand]
    private void ToggleView()
    {
        ShowTargetView = !ShowTargetView;
        ViewLabel = Loc.T(ShowTargetView ? "view.target" : "view.run");
        rowsById.Clear();
        Players.Clear();
        Skills.Clear();
        Refresh();
    }

    [RelayCommand]
    private void SelectPlayer(PlayerRow row)
    {
        selectedPlayerId = selectedPlayerId == row.PlayerId ? null : row.PlayerId;
        ShowDetails = selectedPlayerId is not null;
        Refresh();
    }

    [RelayCommand]
    private void ToggleDetails()
    {
        ShowDetails = !ShowDetails;
        if (ShowDetails && selectedPlayerId is null)
            selectedPlayerId = Players.FirstOrDefault(p => p.IsUser)?.PlayerId
                               ?? Players.FirstOrDefault()?.PlayerId;
        Refresh();
    }

    [RelayCommand]
    private void Reset()
    {
        sessions.Reset();
        if (mode.IsActive) run.Start();
        rowsById.Clear();
        Players.Clear();
        Skills.Clear();
        selectedPlayerId = null;
        Refresh();
    }

    /// <summary>Kurze Meldung in der Statuszeile (8 s).</summary>
    public void ShowNotice(string text)
    {
        Notice = text;
        noticeUntil = DateTime.Now.AddSeconds(8);
        Refresh();
    }

    [RelayCommand]
    public void ReportProblem()
    {
        var dir = Path.Combine(options.DataDir, "Mitschnitte");
        var path = recorder.SaveSnapshot(dir, "Problem melden", typeof(MainViewModel).Assembly.GetName().Version?.ToString() ?? "?");
        Notice = path is null ? Loc.T("report.empty") : Loc.T("report.saved", Path.GetFileName(path));
        noticeUntil = DateTime.Now.AddSeconds(8);
        if (path is not null) OpenFolder(dir);
        Refresh();
    }

    [RelayCommand]
    private void OpenSetup() => SetupRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    public event EventHandler? SettingsRequested;

    /// <summary>Overlay-Knopf „Npcap installieren“ → App öffnet das Einrichtungsfenster.</summary>
    public event EventHandler? SetupRequested;

    public void OpenDataFolder() => OpenFolder(options.DataDir);

    private static void OpenFolder(string dir)
    {
        Directory.CreateDirectory(dir);
        Open(dir);
    }

    private static void Open(string target) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
