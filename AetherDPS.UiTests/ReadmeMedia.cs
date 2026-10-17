using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using AetherDPS.App;
using AetherDPS.App.Services;
using AetherDPS.App.ViewModels;
using AetherDPS.App.Views;
using AetherDPS.Engine;
using AetherDPS.Engine.Capture;
using AetherDPS.Engine.Community;
using AetherDPS.Engine.Testing;
using AetherDPS.Engine.Updates;
using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.PacketCapture;
using AionDpsMeter.Services.Services.Entity;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;

namespace AetherDPS.UiTests;

/// <summary>
/// Bilder fürs README (tools/readme-media.py setzt sie zusammen). Läuft nur mit AETHERDPS_README=&lt;Ordner&gt;,
/// sonst passiert nichts. Mit AETHERDPS_README_FROM=&lt;Datenordner&gt; kommen eigener Charakter und Pet-Stufen
/// aus einem echten Meter-Datenordner; das Profil wird dann wirklich bei NCSOFT geladen (wie im Reiter Profil),
/// Mitspieler bekommen Pseudonyme und das eigene Profil. Ohne FROM bleibt alles erfunden und offline.
/// Gerendert wird in doppelter Auflösung.
/// </summary>
public sealed class ReadmeMedia : IDisposable
{
    private const double Scale = 2;
    private const int Boss = 34083, Ruthilis = 2300708;
    private static readonly string? Out = Environment.GetEnvironmentVariable("AETHERDPS_README") is { Length: > 0 } o ? o : null;
    private static readonly string? From = Environment.GetEnvironmentVariable("AETHERDPS_README_FROM") is { Length: > 0 } f ? f : null;

    /// <summary>Mitspieler: MMO-typische Pseudonyme, Klasse über den Skill-Code (11 Gladiator … 18 Kantor).</summary>
    private static readonly (int Id, string Name, int Class, int Cp, double Power)[] Party =
    [
        (202, "Nachtfalke", 13, 74_120, 1.10),
        (203, "Lunaria", 15, 71_860, 1.04),
        (204, "Kaelthar", 11, 69_340, 0.92),
        (205, "Morgrim", 12, 70_510, 0.58),
        (206, "Seraphyne", 17, 66_980, 0.36),
    ];

    private static readonly int[][] Skills =
    [
        [], [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [11010000, 11020000, 11030000, 11050000, 11090000],
        [12010000, 12020000, 12040000, 12090000],
        [13010000, 13030000, 13050000, 13060000, 13070000],
        [14010000, 14020000, 14030000, 14040000, 14060000, 14080000],
        [15010000, 15020000, 15040000, 15050000, 15060000],
        [16010000, 16020000, 16040000],
        [17010000, 17020000, 17040000, 17050000],
        [18010000, 18020000, 18030000],
    ];

    private readonly string dataDir = Path.Combine(Path.GetTempPath(), "aetherdps-readme", Guid.NewGuid().ToString("N"));
    private ServiceProvider? provider;
    private long clock = 1_000;
    private string selfName = "Elyndra";
    private string selfServer = "#1317";
    private int selfClass = 14;
    private const int SelfId = 201;
    private readonly Random rng = new(7);

    private ServiceProvider Start()
    {
        Loc.Load("de");
        GameText.Apply("de");
        Directory.CreateDirectory(dataDir);
        Environment.CurrentDirectory = dataDir;
        if (From is not null)
        {
            var real = MeterSettings.Load(From);
            if (!string.IsNullOrWhiteSpace(real.CharacterName)) selfName = real.CharacterName!;
            if (!string.IsNullOrWhiteSpace(real.CharacterServer)) selfServer = real.CharacterServer!;
            if (real.CharacterClassId is >= 11 and <= 18) selfClass = real.CharacterClassId;
            foreach (var file in new[] { "pet_stufen.json" })
                if (File.Exists(Path.Combine(From, file))) File.Copy(Path.Combine(From, file), Path.Combine(dataDir, file), true);
        }
        var settings = MeterSettings.Load(dataDir);
        settings.PartyOnly = false;
        settings.CharacterName = selfName;
        settings.CharacterClassId = selfClass;
        settings.CharacterServer = selfServer;
        settings.PetFilter = "open";
        settings.PetRadarWidth = 300;      // Radar kompakt (sonst so groß wie die Spielkarte)
        settings.PetRadarMapHeight = 240;
        settings.Save();
        WriteRuns();
        WriteChecklist();
        var replay = Path.Combine(dataDir, "leer.txt");
        CaptureFile.Write(replay, []);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAetherDPS(new MeterOptions { DataDir = dataDir, ReplayFile = replay });
        services.AddSingleton<IPacketCaptureDevice>(new ShowcaseCapture());   // „Verbunden“ statt „Wiedergabe“
        services.AddSingleton(new UpdateService(dataDir, new Version(0, 7, 4)));
        services.AddAetherDPSUi();
        services.AddSingleton(From is null ? new Armory(new FakeNcsoft()) : new Armory(new AliasHandler(selfName)));
        provider = services.BuildServiceProvider();
        App.App.Services = provider;
        provider.GetRequiredService<MainViewModel>();
        provider.GetRequiredService<Pets>();
        Feed(SyntheticPackets.Heartbeat());
        Entities.SetSessionPlayerName(SelfId, selfName, 72_435, selfServer, isUser: true);
        foreach (var p in Party) Entities.SetSessionPlayerName(p.Id, p.Name, p.Cp, selfServer);
        return provider;
    }

    /// <summary>Hauptfenster am Ort, der zur alten Reiter-Nummer gehört.</summary>
    private ShellWindow Main(int tab, double height)
    {
        var shell = Get<ShellViewModel>();
        shell.Show(tab);
        return new ShellWindow(shell) { Height = height };
    }

    private EntityTracker Entities => provider!.GetRequiredService<EntityTracker>();
    private T Get<T>() where T : notnull => provider!.GetRequiredService<T>();

    private void Feed(byte[] packet) =>
        Get<TcpStreamBuffer>().AddData("test:1->test:2", packet, clock += 100);

    /// <summary>Paket zur aktuellen Kampfzeit (ohne die Uhr weiterzudrehen).</summary>
    private void FeedAt(byte[] packet) =>
        Get<TcpStreamBuffer>().AddData("test:1->test:2", packet, clock);

    [AvaloniaFact]
    public void Readme_Bilder()
    {
        if (Out is null) return;
        Directory.CreateDirectory(Out);
        Start();
        Runs();
        Fight();
        Tabs();
        Pets();
        Timers();
        PartyJoin();
        Extras();
        File.WriteAllText(Path.Combine(Out!, "layout.json"), JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Lage einzelner Stellen im Bild (Pixel in doppelter Auflösung), für die Ausschnitte in tools/readme-media.py.</summary>
    private readonly Dictionary<string, int[]> layout = [];

    private void Mark(Window window, string name, Control control)
    {
        if (control.TranslatePoint(new Point(0, 0), window) is not { } p) return;
        layout[name] = [(int)(p.X * Scale), (int)(p.Y * Scale), (int)((p.X + control.Bounds.Width) * Scale), (int)((p.Y + control.Bounds.Height) * Scale)];
    }

    /// <summary>Abschnittstitel (TextBlock.section) mit diesem Text.</summary>
    private void MarkSection(Window window, string name, string key)
    {
        if (window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains("section") && t.Text == Loc.T(key) && t.IsEffectivelyVisible) is { } t)
            Mark(window, name, t);
    }

    // ── Kampf: Overlay als Bildfolge (GIF) ────────────────────────────────────────────────

    private void Fight()
    {
        Get<TrackingMode>().Activate();
        const long maxHp = 96_000_000;
        long hp = maxHp;
        Feed(SyntheticPackets.MobInfo(Boss, Ruthilis, maxHp));
        var vm = Get<MainViewModel>();
        var overlay = new MainWindow(vm, Get<AionDpsMeter.Services.Services.Settings.IAppSettingsService>());
        overlay.Show();
        var frames = Path.Combine(Out!, "frames_overlay");
        Directory.CreateDirectory(frames);
        var fighters = new List<(int Id, int Class, double Power)> { (SelfId, selfClass, 1.0) };
        fighters.AddRange(Party.Select(p => (p.Id, p.Class, p.Power)));
        int stagger = 317;
        for (int second = 0; second < 48; second++)
        {
            var secondEnds = DateTime.Now.AddSeconds(1);   // Echtzeit: die Kampfuhr im Overlay läuft mit der PC-Uhr
            for (int tick = 0; tick < 10; tick++)
            {
                clock += 100;
                foreach (var (id, cls, power) in fighters)
                {
                    if (rng.NextDouble() > 0.28) continue;
                    var skills = Skills[cls];
                    int skill = skills[rng.Next(skills.Length)];
                    bool crit = rng.NextDouble() < 0.3;
                    long dmg = (long)(21_000 * power * (0.7 + rng.NextDouble() * 0.6) * (crit ? 1.9 : 1) * (1 + second / 60.0));
                    FeedAt(SyntheticPackets.Damage(Boss, id, skill, dmg, crit));
                    hp = Math.Max(maxHp / 12, hp - dmg * 5);
                }
                FeedAt(SyntheticPackets.RemainHp(Boss, hp));
            }
            if (second >= 18 && stagger > 0)
            {
                stagger = Math.Max(0, stagger - 23);
                FeedAt(SyntheticPackets.Stagger(Boss, 317, stagger, 2));
                if (stagger == 0) FeedAt(SyntheticPackets.StaggerBroken(Boss));
            }
            FeedAt(SyntheticPackets.Heartbeat());
            while (DateTime.Now < secondEnds) Settle(0.05);
            vm.Refresh();
            Snap(overlay, Path.Combine("frames_overlay", $"{second:D3}"), keepOpen: true);
        }
        Snap(overlay, "overlay", keepOpen: true);
        vm.ToggleDetailsCommand.Execute(null);
        vm.Refresh();
        Snap(overlay, "overlay_skills", keepOpen: true);
        vm.ToggleDetailsCommand.Execute(null);
        overlay.Close();
    }

    /// <summary>Läufe-Archiv vor dem Kampf (sonst steht „Laufender Lauf“ oben), Bestzeit ausgewählt.</summary>
    private void Runs()
    {
        var hub = Get<HubViewModel>();
        var window = Main(HubViewModel.TabRuns, 900);
        hub.Refresh();
        Settle(0.5);
        if (hub.Runs.Runs.FirstOrDefault(r => r.IsBest) is { } best) hub.Runs.SelectedRun = best;
        Settle(0.5);
        Snap(window, "hub_runs");
    }

    // ── Hauptfenster ─────────────────────────────────────────────────────────────────────

    private void Tabs()
    {
        var hub = Get<HubViewModel>();
        var monitor = Get<ConnectionMonitor>();
        var now = DateTime.Now;
        for (int s = 600; s > 0; s -= 2)
        {
            int ms = 24 + rng.Next(9) + (s is > 300 and < 312 ? 140 + rng.Next(80) : 0) + (s is > 120 and < 124 ? 70 : 0);
            monitor.Add(new PingSample(now.AddSeconds(-s), ms));
        }
        foreach (var (tab, name, height) in new[]
                 {
                     (HubViewModel.TabDps, "hub_now", 660),
                     (HubViewModel.TabChecklist, "hub_checklist", 660), (HubViewModel.TabConnection, "hub_connection", 580),
                     (HubViewModel.TabSettings, "hub_settings", 900), (HubViewModel.TabParty, "hub_party", 660),
                 })
        {
            var window = Main(tab, height);
            hub.Refresh();
            window.Show();
            Settle(1);
            if (tab == HubViewModel.TabConnection
                && window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("sheet") && b.IsEffectivelyVisible) is { } sheet)
            {
                sheet.BoxShadow = default;   // mit Schatten zeichnet RenderTargetBitmap bei doppelter Auflösung den Inhalt doppelt skaliert
                Mark(window, "connection_sheet", sheet);
            }
            Snap(window, name);
        }

        // Profil: echt (bei FROM) inklusive Bilder
        var profileWindow = Main(HubViewModel.TabProfile, 2900);
        hub.Refresh();
        profileWindow.Show();
        var profile = hub.Profile;
        WaitFor(() => profile.HasProfile, 30);
        Settle(8);   // Bilder
        MarkSection(profileWindow, "profile_gear", "profile.gear");
        MarkSection(profileWindow, "profile_stats", "profile.stats");
        MarkSection(profileWindow, "profile_daevanion", "profile.daevanion");
        MarkSection(profileWindow, "profile_skills", "profile.skills");
        Snap(profileWindow, "hub_profile", keepOpen: true);
        if (profile.Armor.Count > 0)
        {
            profile.SelectItemCommand.Execute(profile.Armor[0]);
            WaitFor(() => profile.HasDetail, 20);
            Settle(3);
            MarkSection(profileWindow, "item_gear", "profile.gear");
            MarkSection(profileWindow, "item_stats", "profile.stats");
            Snap(profileWindow, "hub_profile_item", keepOpen: true);
        }
        profileWindow.Close();
    }

    // ── Pets ─────────────────────────────────────────────────────────────────────────────

    private void Pets()
    {
        var pets = Get<Pets>();
        if (!pets.KnowsLevels)
        {
            // ohne echten Datenordner: ein paar Stufen erfinden
            var all = pets.All.Take(120).Select((p, i) => (p.Id, i % 3 == 0 ? 0 : 1 + i % 4, i * 3 % 40)).ToArray();
            Feed(SyntheticPackets.PetList(1077, all));
        }
        Feed(SyntheticPackets.MapLoad(1010));
        Feed(SyntheticPackets.EffectOn(5001, SelfId, 81_840, -20_010));
        Settle(1);

        var hub = Get<HubViewModel>();
        var window = Main(HubViewModel.TabPets, 760);
        hub.Refresh();
        window.Show();
        Settle(1);
        var vm = hub.Pets;
        var pick = vm.Rows.Where(r => !r.Done && r.Spawns > 2).OrderByDescending(r => r.Spawns).Skip(3).FirstOrDefault() ?? vm.Rows.FirstOrDefault();
        if (pick is not null) vm.Selected = pick;
        Settle(1);
        Snap(window, "hub_pets", keepOpen: true);
        window.Close();

        var radar = Get<PetRadarViewModel>();
        Feed(SyntheticPackets.FieldBossList(1010));   // Spielkarte offen → Radar
        Settle(1);
        var settings = Get<MeterSettings>();
        var radarWindow = new PetRadarWindow(radar, settings);
        Snap(radarWindow, "pet_radar");
        settings.PetSpecies = "Feral";
        radar.Refresh();
        Snap(new PetRadarWindow(radar, settings), "pet_radar_species");
        settings.PetSpecies = null;
        radar.Refresh();
        if (radar.Rows.Count > 1)
        {
            radar.SelectCommand.Execute(radar.Rows[1]);
            Snap(new PetRadarWindow(radar, settings), "pet_radar_pick");
            radar.ShowRadarCommand.Execute(null);
        }
        RadarWalk(radar, settings);
    }

    /// <summary>Radar unterwegs (GIF): du fliegst los, die Minimap folgt, die Entfernungen laufen mit; zwischendurch ein Pet angeklickt.</summary>
    private void RadarWalk(PetRadarViewModel radar, MeterSettings settings)
    {
        settings.PetRadarMeters = 220;
        var window = new PetRadarWindow(radar, settings);
        window.Show();
        Directory.CreateDirectory(Path.Combine(Out!, "frames_radar"));
        double x = 81_840, y = -20_010;
        for (int i = 0; i < 70; i++)
        {
            // geschwungener Weg nach Nordosten, ~2 m je Bild
            double angle = 0.9 + Math.Sin(i / 14.0) * 0.6;
            x += Math.Cos(angle) * 200;
            y -= Math.Sin(angle) * 200;
            Feed(SyntheticPackets.EffectOn(5001, SelfId, (float)x, (float)y));
            Dispatcher.UIThread.RunJobs();
            if (i == 42 && radar.Rows.Count > 2) radar.SelectCommand.Execute(radar.Rows[2]);
            if (i == 62) radar.ShowRadarCommand.Execute(null);
            Snap(window, Path.Combine("frames_radar", $"{i:D3}"), keepOpen: true);
        }
        window.Close();
        settings.PetRadarMeters = null;
    }

    // ── Timer ────────────────────────────────────────────────────────────────────────────

    private void Timers()
    {
        var now = DateTimeOffset.UtcNow;
        long At(double minutes) => now.AddMinutes(minutes).ToUnixTimeMilliseconds();
        Feed(SyntheticPackets.FieldBossList(1010,
            (101001, false, At(7.4)), (101002, false, At(23)), (101003, true, At(-12)), (101004, false, At(48)),
            (101005, true, At(-3)), (101006, false, At(71)), (101007, false, At(96)), (101008, true, At(-40))));
        var bosses = Get<FieldBosses>();
        foreach (var map in bosses.Maps.Where(m => m.Id == 1010))
            foreach (var b in map.Bosses.Take(3))
                bosses.SetFavorite(b.Npc, true);
        var service = Get<TimerService>();
        foreach (var (name, min) in new[] { ("Gildenboss", 42), ("Buff-Essen", 28) })
        {
            service.AddCustom(name, min);
            service.StartCustom(name);
        }
        Settle(1);

        var hub = Get<HubViewModel>();
        hub.SelectedTab = HubViewModel.TabTimers;
        hub.Refresh();
        Settle(1);
        foreach (var group in hub.Timers.FieldBossGroups.Take(1)) group.Expanded = true;   // Karte mit den Lieblingsbossen
        var window = Main(HubViewModel.TabTimers, 1250);
        window.Show();
        Settle(1);
        Snap(window, "hub_timers");

        var vm = Get<TimerOverlayViewModel>();
        Dispatcher.UIThread.RunJobs();
        var pills = new TimerPills(vm, service);
        pills.ShowAll();
        Settle(1);
        // einzeln
        var singles = Path.Combine(Out!, "pills");
        Directory.CreateDirectory(singles);
        int n = 0;
        foreach (var w in pills.Windows.ToList())
            Snap(w, Path.Combine("pills", $"{n++:D2}"), keepOpen: true);
        // vier davon als 2×2-Block
        var four = pills.Windows.Take(4).ToList();
        var spots = new[] { (100, 100), (258, 104), (96, 152), (262, 150) };
        for (int i = 0; i < four.Count; i++) four[i].Position = new PixelPoint(spots[i].Item1, spots[i].Item2);
        Dispatcher.UIThread.RunJobs();
        if (four.Count == 4)
        {
            pills.GroupAround(four[0]);
            Settle(1);
            var group = pills.Windows.Single(w => w.Group.Cells.Count > 1);
            Snap(group, "pills_group", keepOpen: true);
        }
        pills.HideAll();
    }

    // ── Gruppenbeitritt ──────────────────────────────────────────────────────────────────

    private void PartyJoin()
    {
        var vm = Get<PartyJoinViewModel>();
        vm.Start();
        Player Member(string name, int power) => new() { Name = name, ServerName = selfServer, CharacterLevel = 45, CombatPower = power };
        Entities.RaisePartyUpdated([Member(selfName, 72_435)]);
        Settle(1);
        Entities.RaisePartyUpdated([Member(selfName, 72_435), Member("Nachtfalke", 74_120), Member("Lunaria", 71_860)]);
        WaitFor(() => vm.Cards.Count >= 2 && vm.Cards.All(c => c.Status != Loc.T("armory.loading", c.Name)), 40);
        Settle(6);
        var window = new PartyJoinWindow();
        window.Attach(vm);
        Snap(window, "party_join");
    }

    // ── Kleinkram ────────────────────────────────────────────────────────────────────────

    private void Extras()
    {
        var toast = new ToastWindow();
        toast.ShowMessage(Loc.T("hint.stagger_fail"),
            Loc.T("hint.stagger_missing", "Gequälter Bakarma", 27, 317) + $"\nNachtfalke 114 · Kaelthar 68 · {selfName} 59 · Seraphyne 18");
        Snap(toast, "toast_stagger");
    }

    // ── Testdaten ────────────────────────────────────────────────────────────────────────

    private void WriteRuns()
    {
        var folder = Directory.CreateDirectory(Path.Combine(dataDir, "Laeufe")).FullName;
        var json = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var day = DateTime.Today.AddHours(20);
        var runs = new (string Dungeon, string[] Bosses, double Minutes, double DayOffset)[]
        {
            ("Forschungsbasis Deus (Schwer)", ["Atiel", "Nazmun", "Gelcos"], 13.9, 0),   // neueste = neue Bestzeit (Jetzt)
            ("Forschungsbasis Deus (Schwer)", ["Atiel", "Nazmun", "Gelcos"], 16.2, -1),
            ("Forschungsbasis Deus (Schwer)", ["Atiel", "Nazmun", "Gelcos"], 14.6, -2),
            ("Arkanis (Normal)", ["Siliator der Täuschung", "Ruthilis des Schmerzes", "Talisra der Leere"], 18.3, -0.1),
            ("Arkanis (Normal)", ["Siliator der Täuschung", "Ruthilis des Schmerzes", "Talisra der Leere"], 19.7, -3),
            ("Limbus (Schwer)", ["Gequälter Bakarma", "Veltiras der Multidimension", "Ehrwürdiger Valkan"], 22.4, -1.2),
        };
        int k = 0;
        foreach (var (dungeon, bossNames, minutes, offset) in runs)
        {
            var start = day.AddDays(offset).AddMinutes(-k * 37);
            double duration = minutes * 60, combat = duration * 0.62;
            var players = new List<ArchivedPlayer>();
            var crew = new List<(long Id, string Name, bool User, int Class, double Power)> { (SelfId, selfName, true, selfClass, 1.0) };
            crew.AddRange(Party.Take(3).Select(p => ((long)p.Id, p.Name, false, p.Class, p.Power)));
            double basis = 98_000 * (1 + rng.NextDouble() * 0.12);
            var dps = crew.Select(c => basis * c.Power * (0.9 + rng.NextDouble() * 0.2)).ToList();
            double total = dps.Sum();
            for (int i = 0; i < crew.Count; i++)
            {
                var c = crew[i];
                long damage = (long)(dps[i] * combat);
                var skillIds = Skills[c.Class];
                var weights = skillIds.Select((_, j) => 1.0 / (j + 1.3)).ToList();
                double wsum = weights.Sum();
                var skills = skillIds.Select((s, j) => new RunSkill(SkillName(s), (long)(damage * weights[j] / wsum),
                    weights[j] / wsum * 100, 40 + rng.Next(160), 22 + rng.NextDouble() * 18)).ToList();
                var buffs = new List<RunBuff> { new("Segen der Kraft", 91.3, 12), new("Heiliger Schild", 64.0, 18), new("Wut", 38.5, 9) };
                players.Add(new ArchivedPlayer(c.Id, c.Name, c.User, c.Class, damage, dps[i], dps[i] / total * 100,
                    c.User && k == 1 ? 1 : 0, 88 + rng.NextDouble() * 10, skills, c.User ? buffs : [], (long)(damage * 0.08)));
            }
            var bossList = new List<RunBoss>();
            double t = 70;
            foreach (var b in bossNames)
            {
                double len = combat / bossNames.Length * (0.8 + rng.NextDouble() * 0.4);
                bossList.Add(new RunBoss(b, TimeSpan.FromSeconds(t), TimeSpan.FromSeconds(len), true, (long)(total * len), 1, (long)(total * len)));
                t += len + duration * 0.38 / bossNames.Length;
            }
            var run = new ArchivedRun(start.ToString("yyyyMMdd_HHmmss"), start, duration, combat, dungeon,
                players.Sum(p => p.Damage), total, players, bossList);
            File.WriteAllText(Path.Combine(folder, run.Id + ".json"), JsonSerializer.Serialize(run, json));
            k++;
        }
    }

    private void WriteChecklist()
    {
        var data = new ChecklistData();
        data.Items.Add(new ChecklistItem { Text = "Gildenspende", Period = ChecklistPeriod.Daily });
        data.Items.Add(new ChecklistItem { Text = "Abyss-Punkte", Period = ChecklistPeriod.Weekly });
        for (int i = 0; i < data.Items.Count; i += 2) data.Items[i].DoneAt = DateTime.Now.AddMinutes(-30);
        File.WriteAllText(Path.Combine(dataDir, "checkliste.json"), JsonSerializer.Serialize(data));
    }

    // ── Hilfen ───────────────────────────────────────────────────────────────────────────

    private static Dictionary<string, string>? skillNames;

    private static string SkillName(int code)
    {
        skillNames ??= JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GameData", "de", "skills.json")))!;
        return skillNames.GetValueOrDefault(code.ToString(), "?");
    }

    private static void Snap(Window window, string name, bool keepOpen = false)
    {
        if (!window.IsVisible) window.Show();
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        var size = window.Bounds.Size;
        var pixels = new PixelSize((int)Math.Ceiling(size.Width * Scale), (int)Math.Ceiling(size.Height * Scale));
        using var bitmap = new RenderTargetBitmap(pixels, new Vector(96 * Scale, 96 * Scale));
        bitmap.Render(window);
        var path = Path.Combine(Out!, name + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path);
        if (!keepOpen) window.Close();
    }

    private static void Settle(double seconds)
    {
        var until = DateTime.Now.AddSeconds(seconds);
        do
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(30);
        } while (DateTime.Now < until);
        Dispatcher.UIThread.RunJobs();
    }

    private static void WaitFor(Func<bool> done, double seconds)
    {
        var until = DateTime.Now.AddSeconds(seconds);
        while (!done() && DateTime.Now < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(50);
        }
    }

    public void Dispose()
    {
        provider?.Dispose();
        try { Directory.Delete(dataDir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Tut so, als liefe ein echter Mitschnitt (Status „Verbunden“).</summary>
    internal sealed class ShowcaseCapture : IPacketCaptureDevice
    {
        public bool IsCapturing => true;
        public string? DeviceName => "Ethernet";
        public void StartCapture() { }
        public void StopCapture() { }
        public void DiscoverAdapters() { }
        public void Dispose() { }
    }

    /// <summary>
    /// Echte NCSOFT-API, aber jeder gesuchte (erfundene) Name bekommt das Profil des eigenen Charakters,
    /// umbenannt und mit anderer Kampfkraft – so stehen in den Bildern nie fremde Spieler.
    /// Armory fragt nacheinander (Suche → Info → Ausrüstung), daher reicht „zuletzt gesuchter Name“.
    /// </summary>
    private sealed class AliasHandler(string realName) : DelegatingHandler(new HttpClientHandler())
    {
        private string? alias;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/search/"))
            {
                var keyword = Uri.UnescapeDataString(url.Split("keyword=")[1].Split('&')[0]);
                alias = string.Equals(keyword, realName, StringComparison.OrdinalIgnoreCase) ? null : keyword;
                if (alias is not null) request.RequestUri = new Uri(url.Replace("keyword=" + Uri.EscapeDataString(alias), "keyword=" + Uri.EscapeDataString(realName)));
            }
            var response = await base.SendAsync(request, ct);
            if (alias is null || !response.IsSuccessStatusCode) return response;
            var body = await response.Content.ReadAsStringAsync(ct);
            body = body.Replace(realName, alias);
            if (url.Contains("/info") && JsonNode.Parse(body) is JsonObject root && root["profile"] is JsonObject p)
            {
                p["combatPower"] = Party.FirstOrDefault(x => x.Name == alias).Cp is > 0 and var cp ? cp : 70_000;
                body = root.ToJsonString();
            }
            return new HttpResponseMessage(response.StatusCode) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
