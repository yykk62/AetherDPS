using AetherDPS.App.Services;
using AetherDPS.App.ViewModels;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AetherDPS.App.Views;

/// <summary>Ein gelöster Timer als eigene kleine Leiste – frei verschiebbar, Position wird gemerkt.</summary>
public partial class TimerBarWindow : Window
{
    private readonly TimerService? service;
    private readonly TimerOverlayViewModel? overlay;
    private readonly DispatcherTimer? saveTimer;

    public TimerBarWindow() => InitializeComponent();

    public TimerBarWindow(TimerRow row, TimerService service, TimerOverlayViewModel overlay, PixelPoint fallback) : this()
    {
        DataContext = row;
        this.service = service;
        this.overlay = overlay;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = service.DetachedPosition(row.Item.Name) is [var x, var y] ? new PixelPoint((int)x, (int)y) : fallback;

        Root.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        saveTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            saveTimer!.Stop();
            SavePosition();
        });
        PositionChanged += (_, _) =>
        {
            saveTimer.Stop();
            saveTimer.Start();
        };
    }

    private void SavePosition()
    {
        if (service is null || DataContext is not TimerRow row || !IsVisible) return;
        if (!service.IsDetached(row.Item.Name)) return;   // inzwischen zurück in der Gruppe
        service.Settings.Detached[row.Item.Name] = [Position.X, Position.Y];
        service.SaveQuiet();
    }

    /// <summary>Gesperrt (über das Schloss der Gruppe) = Klicks gehen ins Spiel.</summary>
    public void SetClickThrough(bool enabled) => ClickThrough.Set(this, enabled);

    private void OnEditClicked(object? sender, RoutedEventArgs e) => overlay?.EditCommand.Execute(null);
}
