using Aion2Meter.App.ViewModels;
using Aion2Meter.App.Views;
using Aion2Meter.Engine;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Services.Services.Session.Persistence;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Aion2Meter.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aion2Meter.App;

public partial class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? desktop;
    private MainWindow? window;
    private MainViewModel? viewModel;
    private SetupWindow? setupWindow;
    private bool meterStarted;

    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            desktop = lifetime;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.ShutdownRequested += (_, _) =>
            {
                if (hub is not null) hub.AllowClose = true;
                Persist();
            };

            // Ohne Npcap geht nichts – dann zuerst die Einrichtung zeigen.
            bool replay = Services.GetRequiredService<MeterOptions>().ReplayFile is not null;
            if (!replay && !Npcap.IsInstalled())
                ShowSetup();
            else
                StartMeter();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void StartMeter()
    {
        if (desktop is null) return;
        if (!meterStarted)
        {
            meterStarted = true;
            _ = Services.GetRequiredService<ICombatHistoryStore>();
            viewModel = Services.GetRequiredService<MainViewModel>();
            viewModel.SetupRequested += (_, _) => ShowSetup();
            viewModel.SettingsRequested += (_, _) => ShowHub(HubTabSettings);
            window = Services.GetRequiredService<MainWindow>();
            hub = Services.GetRequiredService<HubWindow>();

            mode = Services.GetRequiredService<TrackingMode>();
            mode.Changed += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                Serilog.Log.Information("Modus: {Modus}", mode.IsActive ? "aktiv" : "Bereitschaft");
                if (mode.IsActive) window.Show();
                else window.Hide();
            });
            hotkey = new GameHotkey(Services.GetRequiredService<MeterSettings>());
            hotkey.Pressed += (_, _) => Dispatcher.UIThread.Post(() => mode.Toggle());
            hotkey.ScreenshotPressed += (_, foreground) => TakeScreenshot(foreground);
        }
        desktop.MainWindow = hub;
        viewModel!.Start();

        if (Services.GetRequiredService<MeterOptions>().ReplayFile is not null)
        {
            mode!.Activate();   // Wiedergabe: sofort aktiv
            window!.Show();
            return;
        }

        // Start in Bereitschaft: Overlay bleibt aus, das Hauptfenster erklärt den Hotkey.
        ShowHub(HubTabDps);
    }

    private const int HubTabDps = 0, HubTabConnection = 1, HubTabSettings = 2;
    private TrackingMode? mode;
    private GameHotkey? hotkey;
    private HubWindow? hub;

    private void ShowHub(int tab) => hub?.ShowTab(tab);

    private ScreenshotWindow? screenshotWindow;

    /// <summary>Spiel-Bildschirm aufnehmen (Hintergrund-Thread) und auf dem anderen Monitor zeigen.</summary>
    private void TakeScreenshot(IntPtr foreground) => Task.Run(() =>
    {
        ScreenCapture.Shot? shot = null;
        try
        {
            shot = ScreenCapture.CaptureGameMonitor(foreground);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Screenshot fehlgeschlagen");
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (shot is null)
            {
                viewModel?.ShowNotice(Loc.T("shot.failed"));
                return;
            }
            screenshotWindow ??= new ScreenshotWindow();
            if (!screenshotWindow.ShowOnOtherScreen(shot.Image, shot.Monitor))
            {
                shot.Image.Dispose();
                viewModel?.ShowNotice(Loc.T("shot.no_second"));
            }
        });
    });

    private void ShowSetup()
    {
        if (setupWindow is not null)
        {
            setupWindow.Activate();
            return;
        }

        var setupViewModel = Services.GetRequiredService<SetupViewModel>();
        setupWindow = new SetupWindow(setupViewModel);
        EventHandler onCompleted = (_, _) => setupWindow?.Close();
        setupViewModel.Completed += onCompleted;
        setupWindow.Closed += (_, _) =>
        {
            setupViewModel.Completed -= onCompleted;
            setupWindow = null;
            if (Npcap.IsInstalled()) StartMeter();
            else if (!meterStarted) Quit();   // Einrichtung abgebrochen, bevor der Meter je lief
        };
        if (!meterStarted && desktop is not null) desktop.MainWindow = setupWindow;
        setupWindow.Show();
    }

    public void Quit()
    {
        if (hub is not null) hub.AllowClose = true;
        Persist();
        desktop?.Shutdown();
    }

    private bool persisted;

    private void Persist()
    {
        if (persisted) return;
        persisted = true;
        hotkey?.Dispose();
        window?.SaveBounds();
        viewModel?.Stop();
        if (!meterStarted) return;
        try
        {
            Services.GetRequiredService<CombatSessionManager>().CompleteAndPersistActiveSessions();
            Services.GetRequiredService<ICombatHistoryStore>().FlushPendingSaves();
        }
        catch (Exception)
        {
            // Beim Beenden nie hängen bleiben – Historie ist nur ein Extra.
        }
    }

    private void OnTrayClicked(object? sender, EventArgs e) => ShowHub(HubTabDps);

    private void OnToggleOverlay(object? sender, EventArgs e) => mode?.Toggle();

    private void OnToggleClickThrough(object? sender, EventArgs e) =>
        window?.SetClickThrough(!window.IsClickThrough);

    private void OnReport(object? sender, EventArgs e) => viewModel?.ReportProblem();

    private void OnSetup(object? sender, EventArgs e) => ShowSetup();

    private void OnSettings(object? sender, EventArgs e) => ShowHub(HubTabSettings);

    private void OnHub(object? sender, EventArgs e) => ShowHub(HubTabDps);

    private void OnOpenFolder(object? sender, EventArgs e) => viewModel?.OpenDataFolder();

    private void OnQuit(object? sender, EventArgs e) => Quit();
}
