using System.Text.RegularExpressions;
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
using AionDpsMeter.Services.Services.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

[assembly: AvaloniaTestApplication(typeof(AetherDPS.UiTests.TestApp))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AetherDPS.UiTests;

public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia().WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// Rendert alle Fenster unsichtbar (ohne Bildschirm) mit Testdaten. Schlägt fehl, wenn ein Fenster
/// beim Aufbau abstürzt. Mit AETHERDPS_SHOTS=&lt;Ordner&gt; werden Bilder zum Ansehen gespeichert.
/// </summary>
public sealed class UiTests : IDisposable
{
    private const int Boss = 34083, Ruthilis = 2300708;
    private readonly string dataDir = Path.Combine(Path.GetTempPath(), "aetherdps-uitests", Guid.NewGuid().ToString("N"));
    private ServiceProvider? provider;
    private long clock = 1_000;
    private readonly FakeNcsoft ncsoft = new();

    private ServiceProvider Start(string language = "de")
    {
        Loc.Load(language);
        GameText.Apply(language);
        Directory.CreateDirectory(dataDir);
        Environment.CurrentDirectory = dataDir;
        var settings = MeterSettings.Load(dataDir);
        settings.PartyOnly = false;
        settings.CharacterName = "Testheld";
        settings.CharacterClassId = 14;
        settings.CharacterServer = "#1317";
        settings.Save();
        var replay = Path.Combine(dataDir, "leer.txt");
        CaptureFile.Write(replay, []);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAetherDPS(new MeterOptions { DataDir = dataDir, ReplayFile = replay });
        services.AddSingleton(new UpdateService(dataDir, new Version(0, 4, 0)));
        services.AddAetherDPSUi();
        services.AddSingleton(new Armory(ncsoft));   // nie ins Internet
        provider = services.BuildServiceProvider();
        App.App.Services = provider;
        // Pipeline aufbauen (abonniert den Datenstrom), wie beim echten Start
        provider.GetRequiredService<MainViewModel>();
        return provider;
    }

    private void Feed(byte[] packet) =>
        provider!.GetRequiredService<TcpStreamBuffer>().AddData("test:1->test:2", packet, clock += 100);

    private void BossFight()
    {
        provider!.GetRequiredService<TrackingMode>().Activate();
        Feed(SyntheticPackets.Heartbeat());
        Feed(SyntheticPackets.MobInfo(Boss, Ruthilis, 10_681_200));
        var party = new[] { (101, 11000001, 52_000), (102, 14000001, 50_000), (103, 15000001, 41_000), (104, 17000001, 20_000) };
        for (int i = 0; i < 40; i++)
            foreach (var (id, skill, dmg) in party)
                Feed(SyntheticPackets.Damage(Boss, id, skill, dmg + i * 13));
        Feed(SyntheticPackets.RemainHp(Boss, 5_695_920));
    }

    private static void Shot(Window window, string name)
    {
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable("AETHERDPS_SHOTS") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            frame.Save(Path.Combine(dir, name + ".png"));
        }
        window.Close();
    }

    private MainWindow Overlay() =>
        new(provider!.GetRequiredService<MainViewModel>(), provider!.GetRequiredService<IAppSettingsService>());

    [AvaloniaFact]
    public void Overlay_ohne_Kampf_ist_die_Mini_Leiste()
    {
        Start();
        var vm = provider!.GetRequiredService<MainViewModel>();
        vm.Refresh();
        Assert.True(vm.IsCompact);
        Shot(Overlay(), "overlay_mini");
    }

    [AvaloniaFact]
    public void Overlay_im_Bosskampf_mit_Stagger()
    {
        Start();
        BossFight();
        var vm = provider!.GetRequiredService<MainViewModel>();
        vm.Refresh();
        Assert.False(vm.IsCompact);
        Assert.Equal(4, vm.Players.Count);
        Assert.Equal("Ruthilis des Schmerzes", vm.TargetName);
        Shot(Overlay(), "overlay_kampf");

        Feed(SyntheticPackets.Stagger(Boss, 317, 317, 2));
        Feed(SyntheticPackets.Stagger(Boss, 317, 142, 2));
        vm.Refresh();
        Assert.True(vm.StaggerVisible);
        Shot(Overlay(), "overlay_stagger");
    }

    [AvaloniaFact]
    public void Hauptfenster_alle_Reiter()
    {
        Start();
        BossFight();
        var now = DateTimeOffset.UtcNow;
        Feed(SyntheticPackets.FieldBossList(1110, (111001, false, now.AddMinutes(42).ToUnixTimeMilliseconds()),
            (111002, true, now.AddMinutes(-5).ToUnixTimeMilliseconds()), (111003, false, now.AddMinutes(-3).ToUnixTimeMilliseconds())));
        var fieldBosses = provider!.GetRequiredService<FieldBosses>();
        fieldBosses.SetFavorite(fieldBosses.Maps.First(m => m.Id == 1110).Bosses[0].Npc, true);
        var hub = provider!.GetRequiredService<HubViewModel>();
        string[] names = ["dps", "laeufe", "gruppe", "profil", "pets", "timer", "checkliste", "verbindung", "einstellungen"];
        for (int tab = 0; tab < names.Length; tab++)
        {
            var window = new HubWindow(hub);
            hub.SelectedTab = tab;
            hub.Refresh();
            Settle();
            Shot(window, "hub_" + names[tab]);
        }
    }

    [AvaloniaFact]
    public void Timer_Pills_gruppieren_loesen_entfernen()
    {
        Start();
        var service = provider!.GetRequiredService<TimerService>();
        foreach (var (name, min) in new[] { ("Gildenboss", 42), ("Pause", 15), ("Buff-Essen", 30), ("Kiste", 55) })
        {
            service.AddCustom(name, min);
            service.StartCustom(name);
        }
        var vm = provider!.GetRequiredService<TimerOverlayViewModel>();
        Dispatcher.UIThread.RunJobs();
        var pills = new TimerPills(vm, service);
        pills.ShowAll();
        Assert.Equal(vm.Rows.Count, pills.Windows.Count);   // jede Pill allein

        // grob als 2×2 hinlegen und gruppieren
        var spots = new[] { (100, 100), (258, 104), (96, 152), (262, 150) };
        string[] mine = ["Gildenboss", "Pause", "Buff-Essen", "Kiste"];
        var windows = mine.Select(n => pills.Windows.Single(w => w.Group.Cells[0].Name == n)).ToList();
        for (int i = 0; i < 4; i++) windows[i].Position = new PixelPoint(spots[i].Item1, spots[i].Item2);
        Dispatcher.UIThread.RunJobs();
        pills.GroupAround(windows[0]);
        var group = pills.Windows.Single(w => w.Group.Cells.Count > 1);
        Assert.Equal(4, group.Group.Cells.Count);
        Assert.Equal(2, group.Group.Cells.Max(c => c.Col) + 1);
        Assert.Equal(2, group.Group.Cells.Max(c => c.Row) + 1);
        Assert.Equal(vm.Rows.Count - 3, service.Settings.Groups.Count);

        // einzelne Pill komplett entfernen (eigener Timer → weg)
        vm.RemoveTimer(vm.Rows.First(r => r.Item.Name == "Kiste"));
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(service.Items(), i => i.Name == "Kiste");
        group = pills.Windows.Single(w => w.Group.Cells.Count > 1);
        Assert.Equal(3, group.Pills.Count);
        Shot(group, "timer_pills");
    }

    [AvaloniaFact]
    public void Pet_Radar_bei_offener_Karte()
    {
        Start();
        var entities = provider!.GetRequiredService<EntityTracker>();
        provider!.GetRequiredService<Pets>();
        var radar = provider!.GetRequiredService<PetRadarViewModel>();
        bool shown = false;
        radar.ShowRequested += (_, _) => shown = true;
        Feed(SyntheticPackets.Heartbeat());
        entities.SetSessionPlayerName(4613, "Testheld", 0, "#1317", isUser: true);
        Feed(SyntheticPackets.PetList(1077, (1003, 3, 0)));   // kein Kartenwechsel: Meter nach dem Einloggen gestartet
        Feed(SyntheticPackets.Position(4613, 81_840, -20_010));
        Feed(SyntheticPackets.FieldBossList(1010));
        Settle();
        Assert.True(shown);
        Assert.NotNull(radar.Self);
        Assert.Equal(5, radar.Rows.Count);
        Assert.DoesNotContain(radar.Spawns!, s => provider!.GetRequiredService<Pets>().PetOfNpc(s.Npc) == 1003);   // fertig → nicht im Radar
        Assert.All(radar.Rows, r => Assert.NotNull(r.Icon));   // Porträt zu jedem Pet
        Shot(new PetRadarWindow(radar, provider!.GetRequiredService<MeterSettings>()), "pet_radar");

        // Pet anklicken: nur seine Fundorte, alle im Blick
        var pick = radar.Rows[2];
        radar.SelectCommand.Execute(pick);
        Assert.Equal(pick.PetId, radar.SelectedPet);
        Assert.Equal(0, radar.FocusMeters);
        Assert.All(radar.Spawns!, s => Assert.Equal(pick.PetId, provider!.GetRequiredService<Pets>().PetOfNpc(s.Npc)));
        Shot(new PetRadarWindow(radar, provider!.GetRequiredService<MeterSettings>()), "pet_radar_auswahl");
        radar.ShowRadarCommand.Execute(null);
        Assert.Null(radar.SelectedPet);

        // Modus „immer“: auch ohne offene Karte
        radar.Close();
        radar.Mode = PetRadarViewModel.ModeAlways;
        Assert.True(radar.IsOpen);
        radar.Mode = PetRadarViewModel.ModeOff;
        Assert.False(radar.IsOpen);
    }

    [AvaloniaFact]
    public void Stagger_Warnung()
    {
        Start();
        BossFight();
        Feed(SyntheticPackets.Stagger(Boss, 317, 317, 2));
        Feed(SyntheticPackets.Stagger(Boss, 317, 120, 2));
        var vm = provider!.GetRequiredService<StaggerWarningViewModel>();
        vm.Refresh();
        Assert.True(vm.IsActive);
        Shot(new StaggerWarningWindow(vm), "stagger_warnung");
    }

    [AvaloniaFact]
    public void Hinweiskarten_und_Meldefenster()
    {
        Start();
        var hub = provider!.GetRequiredService<HubViewModel>();
        var community = provider!.GetRequiredService<CommunityController>();
        community.Attach(hub, (_, _) => { });
        community.ShowConsent();
        hub.ShowNotice(new HubNotice { Id = "patch", Title = Loc.T("patch.title"), Text = Loc.T("patch.text"), IsWarning = true });
        Assert.Equal(2, hub.Notices.Count);
        hub.DiscordUrl = "https://discord.gg/beispiel";
        Shot(new HubWindow(hub), "hub_hinweise");

        Assert.NotNull(CommunityController.ChangelogSection("0.6.0"));
        Shot(new ReportWindow(provider!.GetRequiredService<ReportViewModel>()), "melden");
    }

    /// <summary>Jede Sprache einmal rendern – lange Texte dürfen nichts sprengen (Bilder mit AETHERDPS_SHOTS ansehen).</summary>
    [AvaloniaFact]
    public void Alle_Sprachen_rendern()
    {
        try
        {
            foreach (var lang in Loc.Available.Where(l => l != "de" && File.Exists(Path.Combine(AppContext.BaseDirectory, "Lang", l + ".json"))))
            {
                Start(lang);
                BossFight();
                var vm = provider!.GetRequiredService<MainViewModel>();
                vm.Refresh();
                Shot(Overlay(), $"{lang}_overlay");
                var hub = provider!.GetRequiredService<HubViewModel>();
                foreach (var (tab, name) in new[] { (HubViewModel.TabDps, "dps"), (HubViewModel.TabSettings, "einstellungen") })
                {
                    var window = new HubWindow(hub);
                    hub.SelectedTab = tab;
                    hub.Refresh();
                    Shot(window, $"{lang}_hub_{name}");
                }
                provider.Dispose();
                provider = null;
            }
        }
        finally
        {
            Loc.Load("de");
            GameText.Apply("de");
        }
    }

    /// <summary>Wartet, bis die (gefälschten) NCSOFT-Antworten verarbeitet sind.</summary>
    private static void Settle()
    {
        for (int i = 0; i < 40; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(30);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Profil_mit_Itemdetails()
    {
        Start();
        var hub = provider!.GetRequiredService<HubViewModel>();
        var profile = hub.Profile;
        hub.SelectedTab = HubViewModel.TabProfile;
        hub.Refresh();
        Settle();
        Assert.True(profile.HasProfile, profile.Status);
        Assert.Equal("Testheld", profile.Name);
        Assert.Equal(3, profile.Armor.Count);
        Assert.Equal(2, profile.Jewelry.Count);
        Assert.Equal(2, profile.GodStats.Count);
        Assert.Equal(3, profile.SkillGroups.Count);
        Assert.Equal("+8.0", profile.AverageEnchant.Replace(',', '.'));

        profile.SelectItemCommand.Execute(profile.Armor[0]);
        Settle();
        Assert.True(profile.HasDetail);
        Assert.Contains(profile.DetailStats, s => s.Value == "223 (+50)");
        Assert.Contains(profile.DetailStats, s => s.Name == Loc.T("profile.empty_slots", 2));
        Shot(new HubWindow(hub), "hub_profil_item");
    }

    [AvaloniaFact]
    public void Pet_Finder_mit_Karte()
    {
        Start();
        var entities = provider!.GetRequiredService<EntityTracker>();
        provider!.GetRequiredService<Pets>();   // wie in App.StartMeter: vor dem ersten Paket
        Feed(SyntheticPackets.Heartbeat());
        entities.SetSessionPlayerName(4613, "Testheld", 0, "#1317", isUser: true);
        Feed(SyntheticPackets.PetList(1077, (1077, 1, 12), (1003, 3, 0), (1007, 2, 40)));
        Feed(SyntheticPackets.MapLoad(1010));
        Feed(SyntheticPackets.Position(4613, -14_591, 20_691));
        var hub = provider!.GetRequiredService<HubViewModel>();
        hub.SelectedTab = HubViewModel.TabPets;
        hub.Refresh();
        Settle();
        var pets = hub.Pets;
        pets.SelectedFilter = pets.FilterOptions[0];   // alle
        Assert.Equal("verteron", pets.Map!.Key);
        Assert.NotEmpty(pets.Rows);
        Assert.NotNull(pets.Self);
        Assert.Contains(pets.Rows, r => r.Pet.Id == 1003 && r.Done);
        pets.Selected = pets.Rows.First(r => r.Pet.Id == 1007);
        Assert.NotEmpty(pets.Spawns!);
        Assert.NotEmpty(pets.Monsters);
        Shot(new HubWindow(hub), "pets_karte");
    }

    [AvaloniaFact]
    public void Gruppenbeitritt_zeigt_neue_Mitglieder()
    {
        Start();
        var vm = provider!.GetRequiredService<PartyJoinViewModel>();
        vm.Start();
        var entities = provider!.GetRequiredService<EntityTracker>();
        Player Member(string name, int power) => new() { Name = name, ServerName = "#1317", CharacterLevel = 45, CombatPower = power };

        entities.RaisePartyUpdated([Member("Testheld", 70_000), Member("Spieler A", 72_435)]);
        Settle();
        Assert.Single(vm.Cards);   // nur der andere, nie man selbst
        Assert.Equal("Spieler A", vm.Cards[0].Name);
        Assert.Equal("1.303", vm.Cards[0].ItemLevel.Replace(",", "."));

        entities.RaisePartyUpdated([Member("Testheld", 70_000), Member("Spieler A", 72_435), Member("Spieler B", 61_000)]);
        Settle();
        Assert.Equal(2, vm.Cards.Count);   // nur Spieler B kam dazu
        Assert.Equal("Spieler B", vm.Cards[0].Name);
        var window = new PartyJoinWindow();
        window.Attach(vm);
        Shot(window, "gruppenbeitritt");

        provider!.GetRequiredService<MeterSettings>().PartyJoinPopup = false;
        vm.Close();
        entities.RaisePartyUpdated([Member("Testheld", 70_000), Member("Spieler C", 50_000)]);
        Settle();
        Assert.Empty(vm.Cards);
    }

    [AvaloniaFact]
    public void Einblendung()
    {
        Start();
        var toast = new ToastWindow();
        toast.ShowMessage(Loc.T("hint.stagger_fail"),
            Loc.T("hint.stagger_missing", "Transzendierter Bakarma", 27, 317) + "\nSpieler A 114 · Spieler B 68 · Du 59 · Spieler C 18");
        Shot(toast, "einblendung");
    }

    [Fact]
    public void Aufraeumen_loescht_nie_den_eigenen_Entpack_Ordner()
    {
        // Nach dem Update aus 0.5.0 heißt die exe weiter Aion2Meter.exe → entpackt nach .net\Aion2Meter\<hash>
        var net = Path.Combine(dataDir, ".net");
        var current = Directory.CreateDirectory(Path.Combine(net, "Aion2Meter", "neu")).FullName;
        File.WriteAllText(Path.Combine(current, "skills.json"), "{}");
        var oldLegacy = Directory.CreateDirectory(Path.Combine(net, "Aion2Meter", "alt")).FullName;
        var oldNew = Directory.CreateDirectory(Path.Combine(net, "AetherDPS", "alt")).FullName;

        LegacyMigration.CleanupExtractions(current + Path.DirectorySeparatorChar);

        Assert.True(File.Exists(Path.Combine(current, "skills.json")));
        Assert.False(Directory.Exists(oldLegacy));
        Assert.False(Directory.Exists(oldNew));
    }

    [Fact]
    public void Alle_Texte_der_Oberflaeche_existieren()
    {
        Loc.Load();
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "AetherDPS.slnx"))) root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        var missing = new List<string>();
        var files = new[] { "AetherDPS.App", "AetherDPS.Engine" }
            .SelectMany(dir => Directory.GetFiles(Path.Combine(root, dir), "*.*", SearchOption.AllDirectories))
            .Where(f => (f.EndsWith(".axaml") || f.EndsWith(".cs")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var keys = Regex.Matches(text, @"(?:l:T |Loc\.T\("")([a-z0-9_]+\.[a-z0-9_.]*[a-z0-9_])\b(?!\.)").Select(m => m.Groups[1].Value)
                // auch Schlüssel in Bedingungen: Loc.T(x ? "a.b" : "c.d")
                .Concat(Regex.Matches(text, @"Loc\.T\([^""()]*\? ""([a-z0-9_.]+)"" : ""([a-z0-9_.]+)""")
                    .SelectMany(m => new[] { m.Groups[1].Value, m.Groups[2].Value }));
            foreach (var key in keys)
                if (Loc.T(key) == key) missing.Add($"{Path.GetFileName(file)}: {key}");
        }
        foreach (var category in new[] { "Riss", "Weltboss", "Event", "Sonstiges" })
            if (Loc.T("timer.cat." + category) == "timer.cat." + category) missing.Add("timer.cat." + category);
        Assert.Empty(missing);
    }

    public void Dispose()
    {
        provider?.Dispose();
        try { Directory.Delete(dataDir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
