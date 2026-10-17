using System.Text.Encodings.Web;
using System.Text.Json;
using AetherDPS.App;
using AetherDPS.App.ViewModels;
using AetherDPS.App.Views;
using AetherDPS.Engine;
using AetherDPS.Engine.Capture;
using AetherDPS.Engine.Community;
using AetherDPS.Engine.Updates;
using AionDpsMeter.Services.PacketCapture;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AetherDPS.UiTests;

/// <summary>
/// Neues Hauptfenster (docs/DESIGN.md), Ort für Ort. Rendert in doppelter Auflösung; mit
/// AETHERDPS_SHOTS=&lt;Ordner&gt; werden die Bilder als shell_*.png gespeichert.
/// </summary>
public sealed class ShellTests : IDisposable
{
    private readonly string dataDir = Path.Combine(Path.GetTempPath(), "aetherdps-shell", Guid.NewGuid().ToString("N"));
    private ServiceProvider? provider;

    /// <summary>Gruppe mit Pseudonymen, Klasse über die ID (11 Gladiator … 18 Kantor).</summary>
    private static readonly (long Id, string Name, bool User, int Class, double Power)[] Crew =
    [
        (201, "Elyndra", true, 14, 1.0),
        (202, "Nachtfalke", false, 13, 1.08),
        (203, "Lunaria", false, 15, 1.03),
        (204, "Kaelthar", false, 11, 0.86),
    ];

    private const int DemoDungeon = 600062;

    private static readonly string[] Skills = ["Zielpfeil", "Scharfschuss", "Schnellfeuer", "Spiralpfeil", "Greifenpfeil", "Sprengpfeil"];
    private static readonly double[] Weights = [0.37, 0.21, 0.15, 0.11, 0.09, 0.07];

    private ServiceProvider Start(bool withRuns, string self = "Elyndra")
    {
        Loc.Load("de");
        GameText.Apply("de");
        Directory.CreateDirectory(dataDir);
        Environment.CurrentDirectory = dataDir;
        var settings = MeterSettings.Load(dataDir);
        settings.CharacterName = self;
        settings.CharacterClassId = 14;
        settings.CharacterServer = "#1317";
        settings.Save();
        if (withRuns)
        {
            WriteRuns();
            WriteHistory(self);
        }
        var replay = Path.Combine(dataDir, "leer.txt");
        CaptureFile.Write(replay, []);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAetherDPS(new MeterOptions { DataDir = dataDir, ReplayFile = replay });
        services.AddSingleton<IPacketCaptureDevice>(new ReadmeMedia.ShowcaseCapture());   // „Verbunden“ statt „Wiedergabe“
        services.AddSingleton(new UpdateService(dataDir, new Version(0, 9, 0)));
        services.AddAetherDPSUi();
        services.AddSingleton(new Armory(new FakeNcsoft()));
        provider = services.BuildServiceProvider();
        App.App.Services = provider;
        provider.GetRequiredService<MainViewModel>();
        return provider;
    }

    [AvaloniaFact]
    public void Jetzt_nach_dem_Lauf()
    {
        Start(withRuns: true);
        var now = provider!.GetRequiredService<NowViewModel>();
        var shell = provider!.GetRequiredService<ShellViewModel>();
        var window = new ShellWindow(shell);
        Assert.True(now.HasResult);
        Assert.True(now.HasHistory);
        Assert.True(now.TimeBest);
        Assert.Equal(4, now.Players.Count);
        Assert.Equal(3, now.Bosses.Count);
        Assert.True(now.Progress.HasData);
        Assert.True(now.Progress.CombatPowerUp);
        Snap(window, "shell_jetzt");
        Snap(new ShellWindow(shell) { Height = 980 }, "shell_jetzt_ganz");
    }

    [AvaloniaFact]
    public void Kaempfe_mit_Laeufen()
    {
        Start(withRuns: true);
        var shell = provider!.GetRequiredService<ShellViewModel>();
        shell.Show(HubViewModel.TabRuns);
        var runs = shell.Hub.Runs;
        Assert.Equal(5, runs.Runs.Count);
        Assert.True(runs.Runs[0].IsSelected);
        Assert.Equal(4, runs.Players.Count);
        Assert.True(runs.HasPlayer);
        Snap(new ShellWindow(shell) { Height = 1060 }, "shell_kaempfe");

        // Löschen mit Rückgängig
        var id = runs.SelectedRun!.Id;
        runs.DeleteRunCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(runs.Runs, r => r.Id == id);
        Assert.True(runs.CanUndo);
        runs.UndoCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(runs.Runs, r => r.Id == id);
    }

    [AvaloniaFact]
    public void Daevas_eigenes_Profil_mit_Verlauf()
    {
        Start(withRuns: true, self: "Testheld");
        var shell = provider!.GetRequiredService<ShellViewModel>();
        shell.Show(HubViewModel.TabProfile);
        var profile = shell.Hub.Profile;
        profile.EnsureLoaded();
        for (int i = 0; i < 60 && !profile.HasProfile; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(50);
        }
        Assert.True(profile.HasProfile);
        Assert.True(profile.IsSelf);
        Assert.True(profile.Progress.HasTrend);
        Snap(new ShellWindow(shell) { Height = 1100 }, "shell_profil_verlauf");
    }

    [AvaloniaFact]
    public void Jetzt_Weltboss_ohne_Vergleich()
    {
        Start(withRuns: true);
        // neuester Lauf: Weltboss in der offenen Welt – darf keine Bestzeit und keinen Vergleich auslösen
        var start = DateTime.Now.AddMinutes(-3);
        var players = new List<ArchivedPlayer> { new(201, "Elyndra", true, 14, 900_000, 15_300, 100, 0, 90, [], [], 0) };
        var run = new ArchivedRun(start.ToString("yyyyMMdd_HHmmss"), start, 63, 41, "Henker Nemok", 900_000, 15_300, players,
            [new RunBoss("Henker Nemok", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(41), true, 900_000, 1, 900_000)], 0, 1010, false);
        File.WriteAllText(Path.Combine(dataDir, "Laeufe", run.Id + ".json"), JsonSerializer.Serialize(run));
        var now = provider!.GetRequiredService<NowViewModel>();
        var shell = provider!.GetRequiredService<ShellViewModel>();
        var window = new ShellWindow(shell);
        Assert.Equal("Henker Nemok", now.RunName);
        Assert.False(now.TimeBest);
        Assert.False(now.HasHistory);
        Snap(window, "shell_jetzt_welt");
    }

    [AvaloniaFact]
    public void Jetzt_ohne_Lauf()
    {
        Start(withRuns: false);
        var now = provider!.GetRequiredService<NowViewModel>();
        var window = new ShellWindow(provider!.GetRequiredService<ShellViewModel>());
        Assert.False(now.HasResult);
        Snap(window, "shell_jetzt_leer");
    }

    /// <summary>Fünf Läufe Arkanis, der letzte vor 20 Minuten und mit neuer Bestzeit.</summary>
    private void WriteRuns()
    {
        var folder = Directory.CreateDirectory(Path.Combine(dataDir, "Laeufe")).FullName;
        var json = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        string[] bosses = ["Siliator der Täuschung", "Ruthilis des Schmerzes", "Talisra der Leere"];
        var runs = new (double Minutes, double UserDps, double DaysAgo)[]
        {
            (19.7, 86_400, 9), (20.4, 90_100, 7), (18.9, 93_800, 5), (19.2, 92_500, 2), (18.3, 98_200, 0),
        };
        var rng = new Random(11);
        foreach (var (minutes, userDps, daysAgo) in runs)
        {
            double duration = minutes * 60, combat = duration * 0.62;
            var start = DateTime.Now.AddDays(-daysAgo).AddMinutes(-20 - minutes);
            var dps = Crew.Select(c => userDps * c.Power * (c.User ? 1 : 0.92 + rng.NextDouble() * 0.16)).ToList();
            double total = dps.Sum();
            var players = Crew.Select((c, i) =>
            {
                long damage = (long)(dps[i] * combat);
                var skills = Skills.Select((n, j) => new RunSkill(n, (long)(damage * Weights[j]), Weights[j] * 100, 60 + rng.Next(140), 24 + rng.NextDouble() * 16)).ToList();
                List<RunBuff> buffs = c.User ? [new("Segen der Kraft", 91.3, 12), new("Heiliger Schild", 64.0, 18), new("Wut", 38.5, 9)] : [];
                return new ArchivedPlayer(c.Id, c.Name, c.User, c.Class, damage, dps[i], dps[i] / total * 100, 0, 92, skills, buffs, (long)(damage * 0.07));
            }).ToList();
            var bossList = new List<RunBoss>();
            double t = 75;
            foreach (var b in bosses)
            {
                double len = combat / bosses.Length * (0.8 + rng.NextDouble() * 0.4);
                long damage = (long)(total * len * (0.85 + rng.NextDouble() * 0.3));
                bossList.Add(new RunBoss(b, TimeSpan.FromSeconds(t), TimeSpan.FromSeconds(len), true, damage, 1, damage));
                t += len + duration * 0.38 / bosses.Length;
            }
            // echter Dungeon (Transzendenz), sonst zählt der Lauf nicht für Vergleiche
            var run = new ArchivedRun(start.ToString("yyyyMMdd_HHmmss"), start, duration, combat, GameText.DungeonName(DemoDungeon) ?? "Arkanis",
                players.Sum(p => p.Damage), total, players, bossList, DemoDungeon, DemoDungeon, true);
            File.WriteAllText(Path.Combine(folder, run.Id + ".json"), JsonSerializer.Serialize(run, json));
        }
    }

    /// <summary>Verlauf des eigenen Charakters über drei Wochen (erfundene Werte).</summary>
    private void WriteHistory(string name = "Elyndra")
    {
        var history = new ProfileHistory(Path.Combine(dataDir, "profil_verlauf.json"));
        var gear = new List<ArmoryItem>
        {
            new("MainHand", "Übungsbogen", "Unique", 10, null), new("Torso", "Übungsharnisch", "Epic", 8, null),
            new("Helmet", "Übungshelm", "Legend", 6, null), new("Ring1", "Übungsring", "Rare", 0, null),
        };
        var start = DateTime.Now.AddDays(-21);
        int[] cp = [68_200, 68_200, 68_900, 69_400, 69_400, 70_300, 70_800, 71_100, 71_900, 72_435];
        for (int i = 0; i < cp.Length; i++)
        {
            var at = start.AddDays(i * 21.0 / (cp.Length - 1));
            history.Add(new ArmoryProfile(name, "Hogalum", 1317, "Waldläufer", 45, 1_260 + i * 5, cp[i], "Elyos", gear, [], at) { ClassId = 14 }, at);
        }
    }

    private static void Snap(Window window, string name)
    {
        const double scale = 2;
        window.Show();
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        var size = window.Bounds.Size;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale)),
            new Vector(96 * scale, 96 * scale));
        bitmap.Render(window);
        if (Environment.GetEnvironmentVariable("AETHERDPS_SHOTS") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            bitmap.Save(Path.Combine(dir, name + ".png"));
        }
        window.Close();
    }

    public void Dispose()
    {
        provider?.Dispose();
        try { Directory.Delete(dataDir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
