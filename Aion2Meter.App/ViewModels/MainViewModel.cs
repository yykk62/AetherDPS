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

    private long? selectedPlayerId;

    public MainViewModel(CombatSessionManager sessions, IPacketService pipeline, IPacketCaptureDevice capture,
        RingRecorder recorder, IAppSettingsService settings, MeterOptions options, ILogger<MainViewModel> logger)
    {
        this.sessions = sessions;
        this.pipeline = pipeline;
        this.capture = capture;
        this.recorder = recorder;
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
        Duration = sessions.GetCombatDuration().ToString(@"mm\:ss");
        PartyDps = Loc.T("footer.party", Format.Compact(sessions.GetPartyDps()));

        var target = sessions.GetActiveTargetInfo();
        HasTarget = target is not null;
        if (target is not null)
        {
            TargetName = target.MobCode == 0 ? Loc.T("target.unknown") : GermanGameData.MobName(target);
            TargetHpPercent = target.HpTotal > 0 ? (double)target.HpCurrent / target.HpTotal * 100 : 0;
        }
        else
        {
            TargetName = Loc.T("target.none");
        }

        UpdatePlayers();
        if (ShowDetails) UpdateSkills();
    }

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

    private void UpdatePlayers()
    {
        var stats = sessions.PlayerStats
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
            row.ClassBrush = ClassColors.For(p.ClassId);
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
    }

    private string DisplayName(PlayerStats p)
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
        var skillStats = sessions.GetPlayerSkillStats(id).OrderByDescending(s => s.TotalDamage).Take(12).ToList();
        long top = skillStats.Count > 0 ? skillStats[0].TotalDamage : 0;
        var rows = skillStats.Select(s => new SkillRow(
            s.SkillName, Format.Compact(s.TotalDamage), Format.Percent(s.DamagePercentage), s.HitCount,
            Format.Percent(s.CriticalRate), top > 0 ? (double)s.TotalDamage / top * 100 : 0)).ToList();

        if (!rows.SequenceEqual(Skills))
        {
            Skills.Clear();
            foreach (var r in rows) Skills.Add(r);
        }
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
            selectedPlayerId = sessions.PlayerStats.FirstOrDefault(p => p.IsUser)?.PlayerId
                               ?? Players.FirstOrDefault()?.PlayerId;
        Refresh();
    }

    [RelayCommand]
    private void Reset()
    {
        sessions.Reset();
        rowsById.Clear();
        Players.Clear();
        Skills.Clear();
        selectedPlayerId = null;
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
