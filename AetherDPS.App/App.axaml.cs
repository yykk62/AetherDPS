using AetherDPS.App.ViewModels;
using AetherDPS.App.Views;
using AetherDPS.Engine;
using AetherDPS.Engine.Updates;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Services.Services.Session.Persistence;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AetherDPS.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AetherDPS.App;

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
            // Fehler in der Oberfläche loggen statt abzustürzen
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Serilog.Log.Error(e.Exception, "Fehler in der Oberfläche");
                e.Handled = true;
            };
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
                Serilog.Log.Information("Modus: {Modus}", mode.AutoMode ? "automatisch" : mode.IsActive ? "aktiv" : "Bereitschaft");
                overlayHiddenByUser = false;
                UpdateOverlay();
            });
            hotkey = new GameHotkey(Services.GetRequiredService<MeterSettings>());
            hotkey.Pressed += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                // Auto-Modus: Hotkey blendet nur ein/aus, erfasst wird sowieso
                if (mode.AutoMode)
                {
                    overlayHiddenByUser = !overlayHiddenByUser;
                    UpdateOverlay();
                }
                else mode.Toggle();
            });
            window.CloseRequested += (_, _) =>
            {
                if (mode.AutoMode)
                {
                    overlayHiddenByUser = true;
                    UpdateOverlay();
                }
                else if (mode.IsActive) mode.Standby();   // blendet über mode.Changed aus
                else window.Hide();
            };
            new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => mode.Tick(DateTime.Now)).Start();
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
                if (timersVm?.OverlayMode == "soon")
                {
                    Services.GetRequiredService<TimerOverlayViewModel>().Tick();
                    UpdateTimerOverlay();
                }
                Services.GetRequiredService<BuffWatcher>().Tick(DateTime.Now);
            }).Start();
            new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) =>
            {
                Services.GetRequiredService<StaggerTracker>().Tick();
                UpdateStaggerWarning(meterSettings);
                bool show = mode.IsCapturing && meterSettings.BuffBarEnabled && window.IsVisible;
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
            timerOverlayVm.HideRequested += (_, _) => timersVm.HideOverlay();
            timersVm.OverlayVisibilityChanged += (_, _) => UpdateTimerOverlay();

            StartCombatHints(meterSettings);
            StartUpdates(meterSettings);
            var hubVm = Services.GetRequiredService<HubViewModel>();
            hubVm.ReportRequested += (_, _) => ShowReport();
            bool replay = Services.GetRequiredService<MeterOptions>().ReplayFile is not null;
            if (!replay)
                Services.GetRequiredService<CommunityController>().Start(hubVm, ShowToast, askConsent: AppUpdate.CanSelfUpdate);
        }
        // Wiedergabe: nur das Overlay, das Hauptfenster öffnet sich nicht von selbst
        bool isReplay = Services.GetRequiredService<MeterOptions>().ReplayFile is not null;
        desktop.MainWindow = isReplay ? window : hub;
        viewModel!.Start();
        UpdateTimerOverlay();

        if (Services.GetRequiredService<MeterOptions>().ReplayFile is not null)
        {
            if (Environment.GetCommandLineArgs().Contains("--auto")) mode!.SetAutoMode(true);   // Test des Auto-Modus
            else mode!.Activate();   // Wiedergabe: sofort aktiv
            if (Environment.GetCommandLineArgs().Contains("--offscreen"))
            {
                // Für Screenshots per PrintWindow, ohne dass das Fenster über einem laufenden Spiel auftaucht:
                // unsichtbar öffnen und erst danach aus dem Bildschirm schieben (vorher setzt Windows es auf 0,0)
                window!.ShowInTaskbar = false;
                window.Opacity = 0;
                window.Opened += (_, _) =>
                {
                    window.Position = new PixelPoint(-6000, -6000);
                    window.Opacity = 1;
                };
            }
            window!.Show();
            return;
        }

        // Start in Bereitschaft: Overlay bleibt aus (im Auto-Modus: Mini-Leiste), das Hauptfenster erklärt den Hotkey.
        UpdateOverlay();
        ShowHub(HubTabDps);
    }

    /// <summary>Im Auto-Modus per Hotkey oder ✕ ausgeblendet.</summary>
    private bool overlayHiddenByUser;

    private void UpdateOverlay()
    {
        if (window is null || mode is null) return;
        bool show = mode.AutoMode ? !overlayHiddenByUser : mode.IsActive;
        if (show && !window.IsVisible) window.Show();
        else if (!show && window.IsVisible) window.Hide();
    }

    private const int HubTabDps = HubViewModel.TabDps, HubTabSettings = HubViewModel.TabSettings;
    private TrackingMode? mode;
    private GameHotkey? hotkey;
    private HubWindow? hub;

    private void ShowHub(int tab)
    {
        // Wiedergaben (Tests) öffnen nie das Hauptfenster – sie laufen oft neben dem echten Meter
        if (Services.GetRequiredService<MeterOptions>().ReplayFile is not null) return;
        hub?.ShowTab(tab);
    }

    private TimersViewModel? timersVm;
    private TimerOverlayWindow? timerOverlay;

    private void UpdateTimerOverlay()
    {
        if (timerOverlay is null || timersVm is null) return;
        // „Nur kurz vorher“: zeigen, sobald ein Termin in den nächsten Minuten ansteht
        bool show = timersVm.OverlayMode switch
        {
            "always" => true,
            "soon" => timerOverlay.HasContent,
            _ => false,
        };
        if (show) timerOverlay.ShowAll();
        else timerOverlay.HideAll();
    }

    private void OnToggleTimerOverlay(object? sender, EventArgs e)
    {
        // Tray: aus ↔ an (an = der zuletzt gewählte Modus, sonst „immer“)
        if (timersVm is null) return;
        if (timersVm.OverlayMode == "off") timersVm.SelectedOverlayMode = timersVm.OverlayModes[0];
        else timersVm.HideOverlay();
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

    private StaggerPhase? lastStagger;
    private StaggerWarningWindow? staggerWarning;

    /// <summary>Große Stagger-Warnung (Einstellung, Standard aus) – nur solange eine Phase läuft.</summary>
    private void UpdateStaggerWarning(MeterSettings settings)
    {
        var vm = Services.GetRequiredService<StaggerWarningViewModel>();
        vm.Refresh();
        bool show = settings.StaggerWarning && vm.IsActive;
        if (show)
        {
            staggerWarning ??= new StaggerWarningWindow(vm);
            if (!staggerWarning.IsVisible) staggerWarning.Show();
        }
        else if (staggerWarning?.IsVisible == true) staggerWarning.Hide();
    }

    /// <summary>Stagger-Ankündigung und -Ergebnis, Todes-Rückblick, auslaufende Buffs – als kleine Einblendung.</summary>
    private void StartCombatHints(MeterSettings settings)
    {
        var stagger = Services.GetRequiredService<StaggerTracker>();
        // Keine Vorab-Ankündigung mit Skill-Tipps – die wären nur für eine Klasse richtig (Rückmeldung 14.10.).
        stagger.Finished += phase => Dispatcher.UIThread.Post(() =>
        {
            lastStagger = phase;
            Serilog.Log.Information("Stagger {Result} {Boss}: Rest {Rest}/{Max}", phase.Success ? "geschafft" : "verfehlt", phase.BossName, phase.Remaining, phase.Max);
            if (settings.CombatHints) ShowToast(Loc.T(phase.Success ? "hint.stagger_ok" : "hint.stagger_fail"), StaggerSummary(phase));
        });
        Services.GetRequiredService<DeathRecap>().Died += report => Dispatcher.UIThread.Post(() =>
        {
            if (!settings.CombatHints) return;
            string text;
            if (report.TotalDamage == 0)
                text = lastStagger is { Success: false } s && DateTime.Now - s.End < TimeSpan.FromSeconds(20)
                    ? Loc.T("hint.death_mechanic_stagger", s.Remaining, s.Max)
                    : Loc.T("hint.death_mechanic");
            else
                text = Loc.T("hint.death_damage", Format.Compact(report.TotalDamage), (int)report.Window.TotalSeconds) + "\n"
                    + string.Join("\n", report.Hits.Take(3).Select(h => $"{h.Source} · {h.Skill}: {Format.Compact(h.Damage)} ({h.Count}×)"));
            ShowToast(Loc.T("hint.death_title"), text);
        });
        Services.GetRequiredService<BuffWatcher>().Expired += name => Dispatcher.UIThread.Post(() =>
        {
            if (settings.CombatHints) ShowToast(Loc.T("hint.buff_title"), Loc.T("hint.buff", name));
        });
    }

    private static string StaggerSummary(StaggerPhase phase)
    {
        var lines = new List<string>
        {
            phase.Success ? phase.BossName : Loc.T("hint.stagger_missing", phase.BossName, phase.Remaining, phase.Max),
            string.Join(" · ", phase.Shares.Take(5).Select(s => $"{(s.IsUser ? Loc.T("player.you") : s.Name)} {s.Points}")),
        };
        if (phase.Shares.FirstOrDefault(s => s.IsUser) is { } me && me.Skills.Count > 0)
            lines.Add(Loc.T("hint.stagger_yours", string.Join(", ", me.Skills.Take(4).Select(k => $"{k.Skill} {k.Points}"))));
        return string.Join("\n", lines);
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
        var startedAt = DateTime.Now;
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            while (true)
            {
                if (settings.AutoUpdate)
                {
                    var state = await updates.CheckAndDownloadAsync();
                    Serilog.Log.Information("Update-Prüfung: {State} (neueste: {Latest}) {Error}",
                        state, updates.Latest?.Version, updates.Error ?? "");
                    // Direkt nach dem Start gefunden: gleich installieren und neu starten –
                    // da läuft noch kein Kampf, und niemand muss in die Einstellungen.
                    if (state == UpdateState.Ready && DateTime.Now - startedAt < TimeSpan.FromMinutes(3))
                        Dispatcher.UIThread.Post(() =>
                        {
                            var run = Services.GetRequiredService<RunTracker>();
                            if (mode is { IsActive: false } && !run.IsRunning)
                            {
                                Serilog.Log.Information("Update beim Start gefunden – installiere und starte neu");
                                RestartForUpdate(onlyIfInstalled: true);
                            }
                        });
                }
                await Task.Delay(TimeSpan.FromHours(1));
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

    /// <summary>
    /// Knopf „Jetzt neu starten“ (Update oder neue Sprache): speichern, ggf. Update einspielen, neu starten.
    /// <paramref name="onlyIfInstalled"/>: automatischer Neustart beim Start – klappt die Installation nicht, weiterlaufen.
    /// </summary>
    private void RestartForUpdate() => RestartForUpdate(onlyIfInstalled: false);

    private void RestartForUpdate(bool onlyIfInstalled)
    {
        bool installed = InstallPendingUpdate();
        if (!installed && onlyIfInstalled) return;
        if (hub is not null) hub.AllowClose = true;
        Persist();
        if (installed) AppUpdate.StartNewVersion();
        else AppUpdate.StartAgain();
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

    private void OnToggleOverlay(object? sender, EventArgs e)
    {
        if (mode is { AutoMode: true })
        {
            overlayHiddenByUser = !overlayHiddenByUser;
            UpdateOverlay();
        }
        else mode?.Toggle();
    }

    private void OnToggleClickThrough(object? sender, EventArgs e) =>
        window?.SetClickThrough(!window.IsClickThrough);

    private void OnReport(object? sender, EventArgs e) => ShowReport();

    private ReportWindow? reportWindow;

    /// <summary>„Problem melden“ (Tray und Hauptfenster).</summary>
    private void ShowReport()
    {
        if (Services.GetRequiredService<MeterOptions>().ReplayFile is not null) return;
        if (reportWindow is { IsVisible: true })
        {
            reportWindow.Activate();
            return;
        }
        reportWindow = new ReportWindow(Services.GetRequiredService<ReportViewModel>());
        reportWindow.Show();
    }

    private void OnSetup(object? sender, EventArgs e) => ShowSetup();

    private void OnSettings(object? sender, EventArgs e) => ShowHub(HubTabSettings);

    private void OnHub(object? sender, EventArgs e) => ShowHub(HubTabDps);

    private void OnOpenFolder(object? sender, EventArgs e) => viewModel?.OpenDataFolder();

    private void OnQuit(object? sender, EventArgs e) => Quit();
}
