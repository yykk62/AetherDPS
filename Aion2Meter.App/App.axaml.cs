using Aion2Meter.App.ViewModels;
using Aion2Meter.App.Views;
using Aion2Meter.Engine;
using Aion2Meter.Engine.Updates;
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

    /// <summary>Gestartet von der alten Version nach einem Update.</summary>
    public static bool JustUpdated { get; set; }

    /// <summary>Sperre „nur ein Meter“ – ein zweiter Start holt das Hauptfenster nach vorn.</summary>
    public static SingleInstance? Instance { get; set; }

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

            Instance?.ListenForShow(() => Dispatcher.UIThread.Post(() =>
            {
                if (hub is not null) ShowHub(HubTabDps);
                else setupWindow?.Activate();
            }));
            AppUpdate.RestartRequested += (_, _) => Dispatcher.UIThread.Post(RestartForUpdate);

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

            // Charakter aus der letzten Sitzung (gilt, solange das Spiel nicht neu gestartet wurde)
            var restored = Services.GetRequiredService<SelfIdentity>().Restore(GameLauncher.StartedAt());
            Serilog.Log.Information("Charakter wiederhergestellt: {Restored}", restored);
            var startSettings = Services.GetRequiredService<MeterSettings>();
            if (startSettings.LaunchGameWithMeter && !GameLauncher.IsRunning
                && Services.GetRequiredService<MeterOptions>().ReplayFile is null)
                GameLauncher.Launch();

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

            // Erinnerungen (Timer, Checkliste) und Buff-Leiste
            var timerService = Services.GetRequiredService<TimerService>();
            var checklist = Services.GetRequiredService<ChecklistService>();
            timerService.Reminder += (title, text) => Dispatcher.UIThread.Post(() => ShowToast(title, text));
            checklist.Reminder += (title, text) => Dispatcher.UIThread.Post(() => ShowToast(title, text));
            var meterSettings = Services.GetRequiredService<MeterSettings>();
            var buffVm = Services.GetRequiredService<BuffBarViewModel>();
            buffBar = new BuffBarWindow(buffVm);
            new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
            {
                timerService.Tick();
                checklist.Tick();
            }).Start();
            new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) =>
            {
                bool show = mode.IsActive && meterSettings.BuffBarEnabled && window.IsVisible;
                if (show) buffVm.Refresh();
                if (show && buffVm.HasItems)
                {
                    buffBar.FollowBelow(window);
                    if (!buffBar.IsVisible) buffBar.Show();
                }
                else if (buffBar.IsVisible)
                {
                    buffBar.Hide();
                }
            }).Start();

            // Timer-Overlay – eigenes Fenster, unabhängig von STRG+W
            timersVm = Services.GetRequiredService<TimersViewModel>();
            var timerOverlayVm = Services.GetRequiredService<TimerOverlayViewModel>();
            timerOverlay = new TimerOverlayWindow(timerOverlayVm, timerService);
            timerOverlayVm.EditRequested += (_, _) => ShowHub(HubViewModel.TabTimers);
            timerOverlayVm.HideRequested += (_, _) => timersVm.OverlayVisible = false;
            timersVm.OverlayVisibilityChanged += (_, _) => UpdateTimerOverlay();

            StartUpdates(meterSettings);
        }
        desktop.MainWindow = hub;
        viewModel!.Start();
        UpdateTimerOverlay();

        if (Services.GetRequiredService<MeterOptions>().ReplayFile is not null)
        {
            mode!.Activate();   // Wiedergabe: sofort aktiv
            window!.Show();
            return;
        }

        // Start in Bereitschaft: Overlay bleibt aus, das Hauptfenster erklärt den Hotkey.
        ShowHub(HubTabDps);
    }

    private const int HubTabDps = HubViewModel.TabDps, HubTabSettings = HubViewModel.TabSettings;
    private TrackingMode? mode;
    private GameHotkey? hotkey;
    private HubWindow? hub;

    private void ShowHub(int tab) => hub?.ShowTab(tab);

    private TimersViewModel? timersVm;
    private TimerOverlayWindow? timerOverlay;

    private void UpdateTimerOverlay()
    {
        if (timerOverlay is null || timersVm is null) return;
        if (timersVm.OverlayVisible && !timerOverlay.IsVisible) timerOverlay.Show();
        else if (!timersVm.OverlayVisible && timerOverlay.IsVisible)
        {
            timerOverlay.SaveBounds();
            timerOverlay.Hide();
        }
    }

    private void OnToggleTimerOverlay(object? sender, EventArgs e)
    {
        if (timersVm is not null) timersVm.OverlayVisible = !timersVm.OverlayVisible;
    }

    private ScreenshotWindow? screenshotWindow;
    private BuffBarWindow? buffBar;
    private ToastWindow? toast;

    private void ShowToast(string title, string text)
    {
        toast ??= new ToastWindow();
        toast.ShowMessage(title, text);
    }

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
        InstallPendingUpdate();   // fertig geladenes Update gleich einspielen, gilt ab dem nächsten Start
        desktop?.Shutdown();
    }

    private bool updateNotified;

    /// <summary>Beim Start und alle 6 Stunden nach einer neuen Version sehen (nur die veröffentlichte exe).</summary>
    private void StartUpdates(MeterSettings settings)
    {
        var updates = Services.GetRequiredService<UpdateService>();
        if (JustUpdated)
            ShowToast(Loc.T("update.done_title"), Loc.T("update.done", updates.Current));
        if (!AppUpdate.CanSelfUpdate || Services.GetRequiredService<MeterOptions>().ReplayFile is not null) return;

        updates.Changed += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (updates.State == UpdateState.Ready && updates.Pending is { } p && !updateNotified)
            {
                updateNotified = true;
                ShowToast(Loc.T("update.ready_title"), Loc.T("update.ready_toast", p.Version));
            }
        });
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(20));
            while (true)
            {
                if (settings.AutoUpdate)
                {
                    var state = await updates.CheckAndDownloadAsync();
                    Serilog.Log.Information("Update-Prüfung: {State} (neueste: {Latest}) {Error}",
                        state, updates.Latest?.Version, updates.Error ?? "");
                }
                await Task.Delay(TimeSpan.FromHours(6));
            }
        });
    }

    private bool InstallPendingUpdate()
    {
        if (!AppUpdate.CanSelfUpdate) return false;
        var updates = Services.GetRequiredService<UpdateService>();
        if (updates.Pending is null) return false;
        try
        {
            updates.Install(AppUpdate.ExePath);
            Serilog.Log.Information("Update installiert, gilt ab dem nächsten Start");
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Update konnte nicht installiert werden");
            return false;
        }
    }

    /// <summary>Knopf „Jetzt neu starten“: speichern, Update einspielen, neue Version starten.</summary>
    private void RestartForUpdate()
    {
        if (hub is not null) hub.AllowClose = true;
        Persist();
        if (InstallPendingUpdate()) AppUpdate.StartNewVersion();
        desktop?.Shutdown();
    }

    private bool persisted;

    private void Persist()
    {
        if (persisted) return;
        persisted = true;
        hotkey?.Dispose();
        window?.SaveBounds();
        timerOverlay?.SaveBounds();
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
