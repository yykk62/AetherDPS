using System.Collections.ObjectModel;
using System.Diagnostics;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using AetherDPS.Engine.Capture;
using AionDpsMeter.Services.Models;
using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.PacketCapture;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Services.Services.Settings;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AetherDPS.App.ViewModels;

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
    private readonly SelfIdentity self;
    private readonly EntityTracker entities;
    private readonly OwnSide own;
    private readonly StaggerTracker stagger;
    private readonly ZoneTracker zones;

    /// <summary>Letztes Ziel, das du oder deine Gruppe getroffen habt (Fremde zählen nicht).</summary>
    private volatile int ownTargetId;
    private long ownTargetAtTicks;
    private static readonly TimeSpan OwnTargetTimeout = TimeSpan.FromSeconds(60);
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
    [ObservableProperty] private bool showNameHint;
    [ObservableProperty] private bool showTargetView;
    [ObservableProperty] private string viewLabel = Loc.T("view.run");

    // Mini-Leiste (kein Kampf) und Kampfkarte
    [ObservableProperty] private bool isCompact = true;
    [ObservableProperty] private string selfName = "";
    [ObservableProperty] private string compactInfo = "";
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? selfIcon;
    [ObservableProperty] private string cardLabel = "";
    [ObservableProperty] private string hpText = "";
    [ObservableProperty] private string hpPercentText = "";
    [ObservableProperty] private bool isBoss;
    [ObservableProperty] private string deathsText = "";
    [ObservableProperty] private string limitText = "";
    [ObservableProperty] private bool limitUrgent;
    [ObservableProperty] private bool hasDeaths;
    [ObservableProperty] private bool staggerVisible;
    [ObservableProperty] private bool showHpBar;
    [ObservableProperty] private double staggerPercent;
    [ObservableProperty] private string staggerText = "";

    /// <summary>Auto-Modus: zuletzt bekämpfter Boss – bleibt nach dem Kampf noch kurz stehen.</summary>
    private TargetCombatSession? shownBoss;
    private DateTime shownBossLastHit;
    private DateTime shownBossSeenAt;
    private TargetCombatSession? expiredBoss;
    private DateTime expiredBossLastHit;
    private static readonly TimeSpan BossLinger = TimeSpan.FromSeconds(45);

    private long? selectedPlayerId;
    private readonly InstanceTimer limit;

    /// <summary>Zeitlimit des Dungeons („42:13“, mit Stoppuhr-Icon), rot in der letzten Minute.</summary>
    private void UpdateLimit()
    {
        if (limit.Current() is not { } t)
        {
            LimitText = "";
            LimitUrgent = false;
            return;
        }
        var left = t.End - DateTime.Now;
        LimitText = (left.TotalHours >= 1 ? left.ToString(@"h\:mm\:ss") : left.ToString(@"mm\:ss"));
        LimitUrgent = left <= TimeSpan.FromMinutes(1);
    }

    public MainViewModel(CombatSessionManager sessions, IPacketService pipeline, IPacketCaptureDevice capture,
        RingRecorder recorder, TrackingMode mode, RunTracker run, PartyRoster roster, MeterSettings meterSettings, TimerService timers, SelfIdentity self,
        EntityTracker entities, OwnSide own, StaggerTracker stagger, ZoneTracker zones, IAppSettingsService settings, MeterOptions options, ILogger<MainViewModel> logger,
        InstanceTimer limit)
    {
        this.limit = limit;
        this.entities = entities;
        this.own = own;
        this.stagger = stagger;
        this.zones = zones;
        sessions.DamageRecorded += hit =>
        {
            if (!own.IsOwn(hit.SourceEntity.Id)) return;
            ownTargetId = hit.TargetEntity.Id;
            Interlocked.Exchange(ref ownTargetAtTicks, DateTime.Now.Ticks);
        };
        this.sessions = sessions;
        this.pipeline = pipeline;
        this.capture = capture;
        this.recorder = recorder;
        this.mode = mode;
        this.run = run;
        this.roster = roster;
        this.meterSettings = meterSettings;
        this.timers = timers;
        this.self = self;
        StandbyText = Loc.T("standby.text", meterSettings.HotkeyText);
        meterSettings.Changed += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            StandbyText = Loc.T("standby.text", meterSettings.HotkeyText);
            Refresh();
        });
        mode.Changed += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            IsStandby = !mode.IsCapturing;
            if (mode.IsActive) { rowsById.Clear(); Players.Clear(); Skills.Clear(); }
            // Auto-Modus zeigt standardmäßig den aktuellen Boss, STRG+W den ganzen Lauf
            SetTargetView(mode.AutoMode);
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

    internal void Refresh()
    {
        UpdateStatus();

        var now = DateTime.Now;
        var snapshot = run.Snapshot();
        var boss = TrackBoss(now);
        Mob? target;
        List<MeterLine> entries;
        if (ShowTargetView)
        {
            // Auto-Modus: der Boss (auch kurz nach seinem Tod), sonst das aktuelle Ziel
            var session = mode.AutoMode ? boss : OwnTargetSession(now);
            target = session is null ? null : entities.GetTargetMob(session.TargetId) ?? session.TargetInfo;
            int attempt = session is null ? 0 : sessions.GetTargetHistory(session.TargetId).Count();
            CardLabel = attempt > 1 ? Loc.T("card.fight_attempt", attempt) : Loc.T("card.fight");
            Duration = FormatDuration(session?.GetCombatDuration() ?? TimeSpan.Zero);
            entries = (session?.GetPlayerStats() ?? [])
                .Select(p => new MeterLine(p.PlayerId, p.PlayerName, p.IsUser, p.IsIdentified, p.ClassId, p.ClassName,
                    p.TotalDamage, p.DamagePerSecond, p.DamagePercentage))
                .ToList();
        }
        else
        {
            target = OwnTargetId(now) is int id ? entities.GetTargetMob(id) : null;
            CardLabel = Loc.T(mode.AutoMode ? "card.run_auto" : "card.run");
            Duration = FormatDuration(snapshot.Elapsed);
            entries = snapshot.Players
                .Select(p => new MeterLine(p.PlayerId, p.Name, p.IsUser, p.IsIdentified, p.ClassId, p.ClassName,
                    p.Damage, p.Dps, p.Share))
                .ToList();
        }

        HasTarget = target is { HpTotal: > 0 };
        IsBoss = target is { MobCode: > 0 } && target.IsBoss;
        TargetHpPercent = target is { HpTotal: > 0 } ? Math.Clamp((double)target.HpCurrent / target.HpTotal * 100, 0, 100) : 0;
        HpText = target is { HpTotal: > 0 } ? Format.Compact(Math.Max(0, target.HpCurrent)) + " / " + Format.Compact(target.HpTotal) : "";
        HpPercentText = target is { HpTotal: > 0 } ? Format.Percent(TargetHpPercent) : "";
        TargetName = target is null ? Loc.T(ShowTargetView ? "target.none" : "run.title_short")
            : target.MobCode == 0 ? Loc.T("target.unknown") : GameText.MobName(target);
        // Stagger-Phase: Leiste auf der Boss-Karte
        if (stagger.Current is { } st)
        {
            StaggerVisible = true;
            StaggerPercent = st.Max > 0 ? st.Current * 100.0 / st.Max : 0;
            StaggerText = Loc.T("card.stagger", st.Current, st.Max);
        }
        else StaggerVisible = false;
        ShowHpBar = HasTarget && !StaggerVisible;
        int deaths = snapshot.Players.Sum(p => p.Deaths);
        HasDeaths = deaths > 0;
        DeathsText = Loc.T("card.deaths", deaths);
        UpdateLimit();

        // Fremde ausblenden (Gruppenmodus) und namenlose Beschwörungen ihrem Besitzer zuschlagen.
        bool userKnown = self.SelfId is not null || entries.Any(e => e.IsUser);
        var merge = MeterFilter.Apply(entries, roster, meterSettings);
        entries = merge.Lines;
        mergedSummons = merge.MergedInto;
        PartyDps = Loc.T("footer.party", Format.Compact(entries.Sum(e => e.DamagePerSecond)));

        UpdatePlayers(entries);
        UpdateSelf();
        // Mini-Leiste, solange nichts zu zeigen ist (Auto-Modus: kein Boss in den letzten Sekunden)
        IsCompact = !ShowDetails && !NpcapMissing && (mode.AutoMode ? boss is null && (ShowTargetView || !HasPlayers) : !HasPlayers);
        // Solange dein Charakter unbekannt ist, erklären, wie er erkannt wird.
        ShowNameHint = !userKnown && !IsCompact;
        if (ShowDetails) UpdateSkills();
    }

    /// <summary>Auto-Modus: welcher Bosskampf gerade angezeigt wird.</summary>
    private TargetCombatSession? TrackBoss(DateTime now)
    {
        if (!mode.AutoMode)
        {
            shownBoss = null;
            return null;
        }
        // Neuer Boss – oder derselbe, der nach dem Zuklappen wieder getroffen wird
        if (OwnTargetSession(now) is { } active && active.TargetInfo.IsBoss && active != shownBoss
            && !(active == expiredBoss && active.LastHitTime == expiredBossLastHit))
        {
            shownBoss = active;
            shownBossLastHit = default;
        }
        if (shownBoss is null) return null;
        // Uhr des PCs statt Paketzeit – so klappt es auch bei Wiedergaben
        if (shownBoss.LastHitTime != shownBossLastHit)
        {
            shownBossLastHit = shownBoss.LastHitTime;
            shownBossSeenAt = now;
        }
        if (now - shownBossSeenAt > BossLinger)
        {
            expiredBoss = shownBoss;
            expiredBossLastHit = shownBoss.LastHitTime;
            shownBoss = null;
        }
        return shownBoss;
    }

    private int? OwnTargetId(DateTime now) =>
        ownTargetId != 0 && now - new DateTime(Interlocked.Read(ref ownTargetAtTicks)) <= OwnTargetTimeout ? ownTargetId : null;

    /// <summary>Kampf gegen das Ziel, das du/deine Gruppe zuletzt getroffen habt.</summary>
    private TargetCombatSession? OwnTargetSession(DateTime now) =>
        OwnTargetId(now) is int id ? sessions.GetTargetHistory(id).LastOrDefault() : null;

    private void UpdateSelf()
    {
        var name = self.Name ?? meterSettings.CharacterName;
        SelfName = string.IsNullOrWhiteSpace(name) ? Loc.T("compact.unknown")
            : settings.IsNicknameHidden ? Loc.T("player.you") : name;
        int classId = self.SelfId is int id && entities.GetPlayerEntity(id)?.CharacterClass?.Id is { } c ? (int)c : meterSettings.CharacterClassId;
        SelfIcon = classId > 0 ? ClassVisuals.Icon(classId) : null;
        var parts = new List<string>();
        var server = !string.IsNullOrWhiteSpace(meterSettings.ServerName) ? meterSettings.ServerName : meterSettings.CharacterServer;
        if (!string.IsNullOrWhiteSpace(server)) parts.Add(EuServers.Display(server));
        parts.Add(!IsConnected ? StatusText : pingMs >= 0 ? Loc.T("status.ping", pingMs) : Loc.T("status.connected"));
        if (zones.InInstance) parts.Add(zones.DungeonName ?? Loc.T("compact.dungeon"));
        UpdateLimit();
        if (LimitText.Length > 0) parts.Add(LimitText);
        parts.Add(Loc.T(mode.AutoMode ? "compact.auto" : "compact.waiting"));
        CompactInfo = string.Join(" · ", parts);
    }

    private void SetTargetView(bool target)
    {
        if (ShowTargetView == target) return;
        ShowTargetView = target;
        ViewLabel = Loc.T(target ? "view.target" : "view.run");
        rowsById.Clear();
        Players.Clear();
        Skills.Clear();
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
            row.ClassName = GameText.ClassName(p.ClassId, p.ClassName);
            row.ClassBrush = ClassVisuals.Brush(p.ClassId);
            row.ClassIcon = ClassVisuals.Icon(p.ClassId);
            row.Dps = Format.Compact(p.DamagePerSecond) + "/s";
            row.Total = Format.Compact(p.TotalDamage);
            row.Share = Format.Percent(p.DamagePercentage);
            row.BarPercent = top > 0 ? (double)p.TotalDamage / top * 100 : 0;
            row.SortKey = p.TotalDamage;
            row.CombatPower = roster.Members.FirstOrDefault(m => m.Name == p.PlayerName) is { CombatPower: > 0 } member
                ? Format.Compact(member.CombatPower) : "";
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
        SetTargetView(!ShowTargetView);
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
