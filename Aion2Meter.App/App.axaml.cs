using Aion2Meter.App.ViewModels;
using Aion2Meter.App.Views;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Services.Services.Session.Persistence;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace Aion2Meter.App;

public partial class App : Application
{
    private MainWindow? window;
    private MainViewModel? viewModel;

    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = Services.GetRequiredService<ICombatHistoryStore>();
            viewModel = Services.GetRequiredService<MainViewModel>();
            window = Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = window;
            window.Show();
            viewModel.Start();
            desktop.ShutdownRequested += (_, _) => Persist();
        }
        base.OnFrameworkInitializationCompleted();
    }

    public void Quit()
    {
        Persist();
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private bool persisted;

    private void Persist()
    {
        if (persisted) return;
        persisted = true;
        window?.SaveBounds();
        viewModel?.Stop();
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

    private void OnTrayClicked(object? sender, EventArgs e) => OnToggleOverlay(sender, e);

    private void OnToggleOverlay(object? sender, EventArgs e)
    {
        if (window is null) return;
        if (window.IsVisible) window.Hide();
        else window.Show();
    }

    private void OnToggleClickThrough(object? sender, EventArgs e) =>
        window?.SetClickThrough(!window.IsClickThrough);

    private void OnReport(object? sender, EventArgs e) => viewModel?.ReportProblem();

    private void OnOpenFolder(object? sender, EventArgs e) => viewModel?.OpenDataFolder();

    private void OnQuit(object? sender, EventArgs e) => Quit();
}
