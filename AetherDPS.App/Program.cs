using System.Globalization;
using AetherDPS.App.Services;
using AetherDPS.App.ViewModels;
using AetherDPS.App.Views;
using AetherDPS.Engine;
using AetherDPS.Engine.Community;
using AetherDPS.Engine.Updates;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AetherDPS.App;

internal static class Program
{
    // Aufruf: AetherDPS.exe [--replay <mitschnitt.txt>] [--speed <faktor>] [--auto] [--offscreen]
    [STAThread]
    public static int Main(string[] args)
    {
        // Wiedergaben sind Tests: eigener, frischer Datenordner, damit Einstellungen, Fensterposition
        // und Kampf-Historie des echten Meters unberührt bleiben.
        var dataDir = ArgValue(args, "--replay") is null
            ? LegacyMigration.MoveDataDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AetherDPS"))
            : Path.Combine(Path.GetTempPath(), "aetherdps-replay");
        Directory.CreateDirectory(dataDir);
        if (ArgValue(args, "--replay") is not null && !File.Exists(Path.Combine(dataDir, "aetherdps.json")))
        {
            // Mitschnitte kennen den eigenen Charakter meist nicht – sonst bliebe die Liste leer
            var replaySettings = MeterSettings.Load(dataDir);
            replaySettings.PartyOnly = false;
            replaySettings.Save();
        }
        // Upstream legt Einstellungen relativ zum Arbeitsverzeichnis ab.
        Environment.CurrentDirectory = dataDir;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File(Path.Combine(dataDir, "logs", "aetherdps-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        // Fehler irgendwo im Hintergrund sollen den Meter nicht beenden – nur ins Log.
        // Echte Abstürze merken: beim nächsten Start wird angeboten, einen Fehlerbericht zu senden.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Unbehandelter Fehler");
            if (e.IsTerminating && e.ExceptionObject is Exception ex) ReportText.WriteCrash(dataDir, ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Fehler in einer Hintergrund-Aufgabe");
            e.SetObserved();
        };

        SingleInstance? instance = null;
        try
        {
            var options = new MeterOptions
            {
                DataDir = dataDir,
                ReplayFile = ArgValue(args, "--replay") is { } r ? Path.GetFullPath(r) : null,
                ReplaySpeed = ArgValue(args, "--speed") is { } s ? double.Parse(s, CultureInfo.InvariantCulture) : 1,
            };
            var version = typeof(Program).Assembly.GetName().Version ?? new Version(0, 0, 0);
            bool justUpdated = args.Contains(AppUpdate.UpdatedArg);
            bool restarted = args.Contains(AppUpdate.RestartArg);

            // Wiedergaben dürfen neben dem echten Meter laufen, sonst nur eine Instanz.
            if (options.ReplayFile is null)
            {
                instance = SingleInstance.TryAcquire(waitForPrevious: justUpdated || restarted);
                if (instance is null)
                {
                    Log.Information("Meter läuft schon – Hauptfenster der laufenden Instanz geöffnet");
                    return 0;
                }
            }

            var updates = new UpdateService(dataDir, version);
            if (AppUpdate.CanSelfUpdate && options.ReplayFile is null)
            {
                UpdateService.CleanupOld(AppUpdate.ExePath);
                Task.Run(() => LegacyMigration.CleanupExtractions(AppContext.BaseDirectory));
                // Beim letzten Mal geladenes Update jetzt einspielen, bevor irgendwas läuft.
                if (updates.Pending is { } pending)
                {
                    try
                    {
                        updates.Install(AppUpdate.ExePath);
                        Log.Information("Update {Old} → {New} installiert, starte neu", version, pending.Version);
                        instance?.Dispose();
                        AppUpdate.StartNewVersion();
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Update konnte nicht installiert werden");
                    }
                }
            }

            var language = Loc.Resolve(MeterSettings.Load(dataDir).Language);
            Loc.Load(language);
            GameText.Apply(language);
            Log.Information("Start {Version}, Sprache {Language} ({Count} Skills übersetzt), Replay: {Replay}, nach Update: {Updated}",
                version, language, GameText.PatchedSkillCount, options.ReplayFile ?? "nein", justUpdated);
            App.JustUpdated = justUpdated;
            App.Instance = instance;

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddSerilog(dispose: false));
            services.AddAetherDPS(options);
            services.AddSingleton(updates);
            services.AddAetherDPSUi();
            App.Services = services.BuildServiceProvider();

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Absturz");
            ReportText.WriteCrash(dataDir, ex);
            return 1;
        }
        finally
        {
            instance?.Dispose();
            Log.CloseAndFlush();
        }
    }

    private static string? ArgValue(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}

public static class AppServices
{
    /// <summary>Fenster und ViewModels – auch von den UI-Tests genutzt.</summary>
    public static IServiceCollection AddAetherDPSUi(this IServiceCollection services)
    {
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<SetupViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<HubViewModel>();
        services.AddSingleton<RunsViewModel>();
        services.AddSingleton<PartyViewModel>();
        services.AddSingleton<ProfileViewModel>();
        services.AddSingleton<PetsViewModel>();
        services.AddSingleton<PetRadarViewModel>();
        services.AddSingleton<PartyJoinViewModel>();
        services.AddSingleton<TimersViewModel>();
        services.AddSingleton<ChecklistViewModel>();
        services.AddSingleton<BuffBarViewModel>();
        services.AddSingleton<TimerOverlayViewModel>();
        services.AddSingleton<HubWindow>();
        services.AddSingleton<StaggerWarningViewModel>();
        services.AddSingleton(sp => new CommunityService(sp.GetRequiredService<MeterSettings>(),
            typeof(Program).Assembly.GetName().Version ?? new Version(0, 0, 0)));
        services.AddSingleton<ReportViewModel>();
        services.AddSingleton<Armory>();
        services.AddSingleton<CommunityController>();
        return services;
    }
}
