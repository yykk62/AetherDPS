using System.Collections.ObjectModel;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using AetherDPS.Engine.Updates;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>Hauptfenster: Reiter DPS-Meter, Verbindung, Einstellungen.</summary>
public sealed partial class HubViewModel : ObservableObject
{
    private readonly TrackingMode mode;
    private readonly RunTracker run;
    private readonly ConnectionMonitor monitor;
    private readonly MeterSettings meterSettings;
    private readonly PartyRoster roster;
    private readonly LiveTimers live;
    private readonly TimerService timerService;
    private readonly UpdateService updates;
    private readonly DispatcherTimer timer;

    public MainViewModel Overlay { get; }
    public SettingsViewModel Settings { get; }
    public RunsViewModel Runs { get; }
    public PartyViewModel Party { get; }
    public ProfileViewModel Profile { get; }
    public PetsViewModel Pets { get; }
    public TimersViewModel Timers { get; }
    public ChecklistViewModel Checklist { get; }

    public const int TabDps = 0, TabRuns = 1, TabParty = 2, TabProfile = 3, TabPets = 4, TabTimers = 5, TabChecklist = 6, TabConnection = 7, TabSettings = 8;
    public ObservableCollection<PlayerRow> RunPlayers { get; } = [];
    public ObservableCollection<DiagnosisRow> DiagnosisRows { get; } = [];

    [ObservableProperty] private int selectedTab;
    [ObservableProperty] private bool isActive;
    [ObservableProperty] private bool isAuto;
    [ObservableProperty] private string modeTitle = "";
    [ObservableProperty] private string modeText = "";
    [ObservableProperty] private string toggleText = "";
    [ObservableProperty] private string runSummary = "";
    [ObservableProperty] private bool hasRun;
    [ObservableProperty] private string characterLine = "";
    [ObservableProperty] private string riftInfo = "";
    [ObservableProperty] private bool gameRunning = true;
    [ObservableProperty] private string launchText = "";
    [ObservableProperty] private string updateBanner = "";
    [ObservableProperty] private string? discordUrl;

    /// <summary>Kopfzeile der gewählten Seite.</summary>
    [ObservableProperty] private string pageTitle = "";
    [ObservableProperty] private string pageSubtitle = "";

    /// <summary>Zahl auf „Übersicht“ in der Navigation, solange Hinweiskarten offen sind.</summary>
    [ObservableProperty] private int noticeCount;
    public bool HasNotices => NoticeCount > 0;
    partial void OnNoticeCountChanged(int value) => OnPropertyChanged(nameof(HasNotices));

    /// <summary>Start/Stopp in der Kopfzeile: auf allen Seiten außer der Übersicht (dort steht der große Knopf).</summary>
    public bool ShowHeaderToggle => !IsAuto && SelectedTab != TabDps;
    partial void OnIsAutoChanged(bool value) => OnPropertyChanged(nameof(ShowHeaderToggle));

    private static readonly (string title, string sub)[] Pages =
    [
        ("nav.overview", "page.overview.sub"), ("hub.tab.runs", "page.runs.sub"), ("hub.tab.party", "page.party.sub"),
        ("hub.tab.profile", "page.profile.sub"), ("hub.tab.pets", "page.pets.sub"), ("hub.tab.timers", "page.timers.sub"),
        ("hub.tab.checklist", "page.checklist.sub"), ("hub.tab.connection", "page.connection.sub"), ("hub.tab.settings", "page.settings.sub"),
    ];

    [RelayCommand]
    private void SelectTab(string? tab)
    {
        if (int.TryParse(tab, out var i) && i >= 0 && i < Pages.Length) SelectedTab = i;
    }

    /// <summary>Versionsplakette im Kopf, z. B. v0.8.1.</summary>
    public string VersionText => "v" + (typeof(HubViewModel).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}" : "?");

    /// <summary>Hinweiskarten oben (Zustimmung, Absturz, Was ist neu, Nachrichten, Spiel-Update).</summary>
    public ObservableCollection<HubNotice> Notices { get; } = [];

    /// <summary>Nach jeder Aktualisierung (jede Sekunde, solange das Hauptfenster offen ist).</summary>
    public event EventHandler? Refreshed;

    /// <summary>„Problem melden“ öffnet das Meldefenster (App).</summary>
    public event EventHandler? ReportRequested;

    [ObservableProperty] private string pingNow = "–";
    [ObservableProperty] private string pingAvg = "–";
    [ObservableProperty] private string pingJitter = "–";
    [ObservableProperty] private string spikeCount = "–";
    [ObservableProperty] private string retransmits = "–";
    [ObservableProperty] private string pingSummary = "";
    [ObservableProperty] private IReadOnlyList<PingSample>? pingSamples;
    [ObservableProperty] private double spikeThreshold = double.MaxValue;
    [ObservableProperty] private bool hasPingData;
    [ObservableProperty] private string serverText = "";
    [ObservableProperty] private bool isDiagnosing;
    [ObservableProperty] private string diagnosisText = "";

    public HubViewModel(TrackingMode mode, RunTracker run, ConnectionMonitor monitor, MeterSettings meterSettings,
        PartyRoster roster, MainViewModel overlay, SettingsViewModel settings,
        RunsViewModel runs, PartyViewModel party, ProfileViewModel profile, PetsViewModel pets, TimersViewModel timers, ChecklistViewModel checklist, LiveTimers live, TimerService timerService, UpdateService updates)
    {
        this.updates = updates;
        this.mode = mode;
        this.run = run;
        this.monitor = monitor;
        this.meterSettings = meterSettings;
        this.roster = roster;
        this.live = live;
        this.timerService = timerService;
        Overlay = overlay;
        Settings = settings;
        Runs = runs;
        Party = party;
        Profile = profile;
        Pets = pets;
        party.ProfileRequested += (name, server) =>
        {
            Profile.Show(name, server);
            SelectedTab = TabProfile;
        };
        Timers = timers;
        Checklist = checklist;
        mode.Changed += (_, _) => Dispatcher.UIThread.Post(Refresh);
        Notices.CollectionChanged += (_, _) => NoticeCount = Notices.Count;
        UpdatePage();
        timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Refresh());
    }

    public void StartRefreshing()
    {
        Refresh();
        timer.Start();
    }

    public void StopRefreshing() => timer.Stop();

    internal void Refresh()
    {
        IsActive = mode.IsCapturing;
        IsAuto = mode.AutoMode;
        ModeTitle = Loc.T(IsAuto ? "hub.mode.auto" : IsActive ? "hub.mode.active" : "hub.mode.standby");
        ModeText = Loc.T(IsAuto ? "hub.mode.auto_text" : IsActive ? "hub.mode.active_text" : "hub.mode.standby_text", meterSettings.HotkeyText);
        ToggleText = Loc.T(IsActive ? "hub.stop" : "hub.start");
        GameRunning = AetherDPS.App.Services.GameLauncher.IsRunning;
        LaunchText = Loc.T(GameRunning ? "hub.launch_running" : "hub.launch");
        UpdateBanner = AppUpdate.CanSelfUpdate && updates.Pending is { } pending ? Loc.T("update.banner", pending.Version) : "";
        var next = timerService.Items().Where(i => i.DueAt > DateTime.Now).MinBy(i => i.DueAt);
        RiftInfo = next is { DueAt: DateTime due }
            ? Loc.T("rift.hub", next.Name, FormatLeft(due - DateTime.Now), due.ToString("HH:mm"))
            : "";
        switch (SelectedTab)
        {
            case TabDps: RefreshRun(); break;
            case TabRuns: Runs.Tick(); break;
            case TabParty: Party.Refresh(); break;
            case TabProfile: Profile.EnsureLoaded(); break;
            case TabPets: Pets.Refresh(); break;
            case TabTimers: Timers.Tick(); break;
            case TabChecklist: Checklist.Refresh(); break;
            case TabConnection: RefreshConnection(); break;
        }
        Refreshed?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshRun()
    {
        var self = live.Diagnostics().Self ?? meterSettings.CharacterName;
        var server = EuServers.Display(!string.IsNullOrWhiteSpace(meterSettings.ServerName) ? meterSettings.ServerName : meterSettings.CharacterServer);
        CharacterLine = string.IsNullOrWhiteSpace(self) ? Loc.T("hub.character_unknown")
            : Loc.T("hub.character", string.IsNullOrEmpty(server) ? self : $"{self} · {server}");
        var snapshot = run.Snapshot();
        var lines = snapshot.Players
            .Select(p => new MeterLine(p.PlayerId, p.Name, p.IsUser, p.IsIdentified, p.ClassId, p.ClassName, p.Damage, p.Dps, p.Share))
            .ToList();
        var players = MeterFilter.Apply(lines, roster, meterSettings).Lines
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
                ClassName = GameText.ClassName(p.ClassId, p.ClassName),
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
        PingSummary = HasPingData ? Loc.T("conn.summary", PingAvg, PingJitter, SpikeCount, Retransmits) : "";
    }

    partial void OnSelectedTabChanged(int value)
    {
        UpdatePage();
        OnPropertyChanged(nameof(ShowHeaderToggle));
        Refresh();
    }

    private void UpdatePage()
    {
        var (title, sub) = Pages[Math.Clamp(SelectedTab, 0, Pages.Length - 1)];
        PageTitle = Loc.T(title);
        PageSubtitle = Loc.T(sub);
    }

    private static string FormatLeft(TimeSpan left) =>
        left.TotalHours >= 24 ? Loc.T("time.days_hours", (int)left.TotalDays, left.Hours) : left.TotalHours >= 1 ? left.ToString(@"h\:mm\:ss") : left.ToString(@"mm\:ss");

    /// <summary>Karte zeigen; eine Karte mit derselben Id wird ersetzt.</summary>
    public void ShowNotice(HubNotice notice)
    {
        notice.Close = new RelayCommand(() => RemoveNotice(notice.Id));
        RemoveNotice(notice.Id);
        Notices.Insert(0, notice);
    }

    public void RemoveNotice(string id)
    {
        foreach (var n in Notices.Where(n => n.Id == id).ToList()) Notices.Remove(n);
    }

    [RelayCommand]
    private void OpenDiscord()
    {
        if (DiscordUrl is { } url) AetherDPS.App.Services.Browser.Open(url);
    }

    [RelayCommand]
    private void LaunchGame() => AetherDPS.App.Services.GameLauncher.Launch();

    [RelayCommand]
    private void ToggleMeter() => mode.Toggle();

    [RelayCommand]
    private void RestartForUpdate() => AppUpdate.RequestRestart();

    [RelayCommand]
    private void ReportProblem() => ReportRequested?.Invoke(this, EventArgs.Empty);

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
