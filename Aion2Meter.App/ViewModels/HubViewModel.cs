using System.Collections.ObjectModel;
using Aion2Meter.App.Services;
using Aion2Meter.Engine;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Meter.App.ViewModels;

/// <summary>Hauptfenster: Reiter DPS-Meter, Verbindung, Einstellungen.</summary>
public sealed partial class HubViewModel : ObservableObject
{
    private readonly TrackingMode mode;
    private readonly RunTracker run;
    private readonly ConnectionMonitor monitor;
    private readonly MeterSettings meterSettings;
    private readonly PartyRoster roster;
    private readonly DispatcherTimer timer;

    public MainViewModel Overlay { get; }
    public SettingsViewModel Settings { get; }
    public ObservableCollection<PlayerRow> RunPlayers { get; } = [];
    public ObservableCollection<DiagnosisRow> DiagnosisRows { get; } = [];

    [ObservableProperty] private int selectedTab;
    [ObservableProperty] private bool isActive;
    [ObservableProperty] private string modeTitle = "";
    [ObservableProperty] private string modeText = "";
    [ObservableProperty] private string toggleText = "";
    [ObservableProperty] private string runSummary = "";
    [ObservableProperty] private bool hasRun;

    [ObservableProperty] private string pingNow = "–";
    [ObservableProperty] private string pingAvg = "–";
    [ObservableProperty] private string pingJitter = "–";
    [ObservableProperty] private string spikeCount = "–";
    [ObservableProperty] private string retransmits = "–";
    [ObservableProperty] private IReadOnlyList<PingSample>? pingSamples;
    [ObservableProperty] private double spikeThreshold = double.MaxValue;
    [ObservableProperty] private bool hasPingData;
    [ObservableProperty] private string serverText = "";
    [ObservableProperty] private bool isDiagnosing;
    [ObservableProperty] private string diagnosisText = "";

    public HubViewModel(TrackingMode mode, RunTracker run, ConnectionMonitor monitor, MeterSettings meterSettings,
        PartyRoster roster, MainViewModel overlay, SettingsViewModel settings)
    {
        this.mode = mode;
        this.run = run;
        this.monitor = monitor;
        this.meterSettings = meterSettings;
        this.roster = roster;
        Overlay = overlay;
        Settings = settings;
        mode.Changed += (_, _) => Dispatcher.UIThread.Post(Refresh);
        timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Refresh());
    }

    public void StartRefreshing()
    {
        Refresh();
        timer.Start();
    }

    public void StopRefreshing() => timer.Stop();

    private void Refresh()
    {
        IsActive = mode.IsActive;
        ModeTitle = Loc.T(IsActive ? "hub.mode.active" : "hub.mode.standby");
        ModeText = Loc.T(IsActive ? "hub.mode.active_text" : "hub.mode.standby_text", meterSettings.HotkeyText);
        ToggleText = Loc.T(IsActive ? "hub.stop" : "hub.start");
        RefreshRun();
        RefreshConnection();
    }

    private void RefreshRun()
    {
        var snapshot = run.Snapshot();
        var lines = snapshot.Players
            .Select(p => new MeterLine(p.PlayerId, p.Name, p.IsUser, p.IsIdentified, p.ClassId, p.ClassName, p.Damage, p.Dps, p.Share))
            .ToList();
        if (meterSettings.PartyOnly)
            lines = lines.Where(l => roster.IsVisible(l.PlayerName, l.IsUser, l.IsIdentified)).ToList();
        var players = SummonMerger.Merge(lines, dropUnmerged: meterSettings.PartyOnly && roster.Count > 0).Lines
            .Where(p => p.IsIdentified || p.DamagePercentage > 1).Take(8).ToList();
        HasRun = players.Count > 0;
        var duration = snapshot.Elapsed.TotalHours >= 1 ? snapshot.Elapsed.ToString(@"h\:mm\:ss") : snapshot.Elapsed.ToString(@"mm\:ss");
        RunSummary = Loc.T("hub.run.summary", duration, Format.Compact(snapshot.TotalDamage), Format.Compact(snapshot.PartyDps));

        long top = players.Count > 0 ? players[0].TotalDamage : 0;
        RunPlayers.Clear();
        foreach (var p in players)
        {
            RunPlayers.Add(new PlayerRow(p.PlayerId)
            {
                Name = p.PlayerName.StartsWith("Player_", StringComparison.Ordinal) ? Loc.T("player.unknown", p.PlayerId) : p.PlayerName,
                ClassName = GermanGameData.ClassName(p.ClassId, p.ClassName),
                ClassBrush = ClassVisuals.Brush(p.ClassId),
                ClassIcon = ClassVisuals.Icon(p.ClassId),
                Dps = Format.Compact(p.DamagePerSecond) + "/s",
                Total = Format.Compact(p.TotalDamage),
                Share = Format.Percent(p.DamagePercentage),
                BarPercent = top > 0 ? p.TotalDamage * 100.0 / top : 0,
                IsUser = p.IsUser,
            });
        }
    }

    private void RefreshConnection()
    {
        var stats = monitor.Stats();
        HasPingData = stats.Current is not null;
        PingNow = stats.Current is int c ? $"{c} ms" : "–";
        PingAvg = stats.Average is double a ? $"{a:0} ms" : "–";
        PingJitter = stats.Jitter is double j ? $"± {j:0} ms" : "–";
        SpikeCount = HasPingData ? stats.Spikes.Count.ToString() : "–";
        Retransmits = stats.Segments > 0 ? Format.Percent(stats.RetransmitPercent) : "–";
        PingSamples = monitor.Samples();
        SpikeThreshold = stats.SpikeThreshold;
        ServerText = monitor.ServerAddress is { } ip ? Loc.T("conn.server", ip) : "";
    }

    [RelayCommand]
    private void ToggleMeter() => mode.Toggle();

    [RelayCommand]
    private void ReportProblem() => Overlay.ReportProblem();

    [RelayCommand]
    private void OpenDataFolder() => Overlay.OpenDataFolder();

    [RelayCommand]
    private async Task DiagnoseAsync()
    {
        if (IsDiagnosing) return;
        IsDiagnosing = true;
        DiagnosisRows.Clear();
        DiagnosisText = Loc.T("conn.checking");
        try
        {
            var d = await monitor.DiagnoseAsync();
            DiagnosisRows.Add(Row(Loc.T(d.IsWifi ? "conn.row.router_wifi" : "conn.row.router"), d.Router, isServer: false));
            DiagnosisRows.Add(Row(Loc.T("conn.row.internet"), d.Internet, isServer: false));
            DiagnosisRows.Add(Row(Loc.T("conn.row.server"), d.GameServer, isServer: true));
            DiagnosisText = Loc.T($"conn.verdict.{d.Verdict}");
        }
        finally
        {
            IsDiagnosing = false;
        }
    }

    private static DiagnosisRow Row(string label, ProbeResult r, bool isServer)
    {
        if (r.Reachable)
            return new DiagnosisRow(label, Loc.T("conn.row.result", $"{r.AverageMs:0}", Format.Percent(r.LossPercent)),
                r.LossPercent == 0);
        return new DiagnosisRow(label, Loc.T(isServer ? "conn.row.server_blocked" : "conn.row.unreachable"), isServer);
    }
}

public sealed record DiagnosisRow(string Label, string Result, bool Ok);
