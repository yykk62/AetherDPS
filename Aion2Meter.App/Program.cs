using System.Globalization;
using Aion2Meter.App.Services;
using Aion2Meter.App.ViewModels;
using Aion2Meter.App.Views;
using Aion2Meter.Engine;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Aion2Meter.App;

internal static class Program
{
    // Aufruf: Aion2Meter.exe [--replay <mitschnitt.txt>] [--speed <faktor>]
    [STAThread]
    public static int Main(string[] args)
    {
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aion2Meter");
        Directory.CreateDirectory(dataDir);
        // Upstream legt Einstellungen relativ zum Arbeitsverzeichnis ab.
        Environment.CurrentDirectory = dataDir;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File(Path.Combine(dataDir, "logs", "aion2meter-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        try
        {
            var options = new MeterOptions
            {
                DataDir = dataDir,
                ReplayFile = ArgValue(args, "--replay") is { } r ? Path.GetFullPath(r) : null,
                ReplaySpeed = ArgValue(args, "--speed") is { } s ? double.Parse(s, CultureInfo.InvariantCulture) : 1,
            };

            Loc.Load();
            GermanGameData.Apply();
            Log.Information("Start {Version}, deutsche Skills: {Count}, Replay: {Replay}",
                typeof(Program).Assembly.GetName().Version, GermanGameData.PatchedSkillCount, options.ReplayFile ?? "nein");

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddSerilog(dispose: false));
            services.AddAion2Meter(options);
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();
            services.AddSingleton<SetupViewModel>();
            App.Services = services.BuildServiceProvider();

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Absturz");
            return 1;
        }
        finally
        {
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
