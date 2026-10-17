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

    private ServiceProvider Start(bool withRuns)
    {
        Loc.Load("de");
        GameText.Apply("de");
        Directory.CreateDirectory(dataDir);
        Environment.CurrentDirectory = dataDir;
        var settings = MeterSettings.Load(dataDir);
        settings.CharacterName = "Elyndra";
        settings.CharacterClassId = 14;
        settings.CharacterServer = "#1317";
        settings.Save();
        if (withRuns) WriteRuns();
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
        Snap(window, "shell_jetzt");
        Snap(new ShellWindow(shell) { Height = 980 }, "shell_jetzt_ganz");
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
            var players = Crew.Select((c, i) => new ArchivedPlayer(c.Id, c.Name, c.User, c.Class, (long)(dps[i] * combat), dps[i],
                dps[i] / total * 100, 0, 92, [], [], 0)).ToList();
            var bossList = new List<RunBoss>();
            double t = 75;
            foreach (var b in bosses)
            {
                double len = combat / bosses.Length * (0.8 + rng.NextDouble() * 0.4);
                long damage = (long)(total * len * (0.85 + rng.NextDouble() * 0.3));
                bossList.Add(new RunBoss(b, TimeSpan.FromSeconds(t), TimeSpan.FromSeconds(len), true, damage, 1, damage));
                t += len + duration * 0.38 / bosses.Length;
            }
            var run = new ArchivedRun(start.ToString("yyyyMMdd_HHmmss"), start, duration, combat, "Arkanis (Normal)",
                players.Sum(p => p.Damage), total, players, bossList);
            File.WriteAllText(Path.Combine(folder, run.Id + ".json"), JsonSerializer.Serialize(run, json));
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
