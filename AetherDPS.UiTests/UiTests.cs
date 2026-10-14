using System.Text.RegularExpressions;
using AetherDPS.App;
using AetherDPS.App.Services;
using AetherDPS.App.ViewModels;
using AetherDPS.App.Views;
using AetherDPS.Engine;
using AetherDPS.Engine.Capture;
using AetherDPS.Engine.Testing;
using AetherDPS.Engine.Updates;
using AionDpsMeter.Services.PacketCapture;
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

    private ServiceProvider Start()
    {
        Loc.Load();
        GameText.Apply();
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
        var hub = provider!.GetRequiredService<HubViewModel>();
        string[] names = ["dps", "laeufe", "gruppe", "timer", "checkliste", "verbindung", "einstellungen"];
        for (int tab = 0; tab < names.Length; tab++)
        {
            var window = new HubWindow(hub);
            hub.SelectedTab = tab;
            hub.Refresh();
            Shot(window, "hub_" + names[tab]);
        }
    }

    [AvaloniaFact]
    public void Timer_Gruppe_und_gelöste_Leiste()
    {
        Start();
        var service = provider!.GetRequiredService<TimerService>();
        service.AddCustom("Gildenboss", 42);
        service.StartCustom("Gildenboss");
        var vm = provider!.GetRequiredService<TimerOverlayViewModel>();
        vm.Tick();
        Shot(new TimerOverlayWindow(vm, service), "timer_gruppe");

        var row = vm.Rows.First(r => r.Item.Name == "Gildenboss");
        row.DetachCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var bar = vm.Detached.Single();
        Assert.DoesNotContain(vm.Rows, r => r.Item.Name == "Gildenboss");
        Shot(new TimerBarWindow(bar, service, vm, new PixelPoint(0, 0)), "timer_leiste");
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
    public void Einblendung()
    {
        Start();
        var toast = new ToastWindow();
        toast.ShowMessage(Loc.T("hint.stagger_fail"),
            Loc.T("hint.stagger_missing", "Transzendierter Bakarma", 27, 317) + "\nSpieler A 114 · Spieler B 68 · Du 59 · Spieler C 18");
        Shot(toast, "einblendung");
    }

    [Fact]
    public void Alle_Texte_der_Oberflaeche_existieren()
    {
        Loc.Load();
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "AetherDPS.slnx"))) root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        var missing = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "AetherDPS.App"), "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".axaml") || f.EndsWith(".cs")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"(?:l:T |Loc\.T\("")([a-z0-9_]+\.[a-z0-9_.]+)"))
                if (Loc.T(m.Groups[1].Value) == m.Groups[1].Value) missing.Add($"{Path.GetFileName(file)}: {m.Groups[1].Value}");
        }
        Assert.Empty(missing);
    }

    public void Dispose()
    {
        provider?.Dispose();
        try { Directory.Delete(dataDir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
