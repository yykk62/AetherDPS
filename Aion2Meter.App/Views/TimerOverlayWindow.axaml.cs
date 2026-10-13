using Aion2Meter.App.Services;
using Aion2Meter.App.ViewModels;
using Aion2Meter.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Aion2Meter.App.Views;

/// <summary>
/// Timer-Overlay: verschiebbar (Kopfzeile ziehen), skalierbar (Rand ziehen), sperrbar wie der
/// DPS-Meter (Schloss bleibt klickbar). Position und Größe merkt es sich in timer.json.
/// </summary>
public partial class TimerOverlayWindow : Window
{
    private readonly TimerSettings? settings;
    private readonly TimerService? service;
    private readonly DispatcherTimer? lockTimer;
    private readonly DispatcherTimer? tickTimer;

    public TimerOverlayWindow() => InitializeComponent();

    public TimerOverlayWindow(TimerOverlayViewModel viewModel, TimerService service) : this()
    {
        DataContext = viewModel;
        this.service = service;
        settings = service.Settings;
        RestoreBounds();

        Header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        LockButton.IsCheckedChanged += (_, _) => SetClickThrough(LockButton.IsChecked == true);
        lockTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(60), DispatcherPriority.Input, (_, _) =>
        {
            if (viewModel.IsClickThrough) ClickThrough.Set(this, !ClickThrough.IsCursorOver(LockButton));
        });
        tickTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => viewModel.Tick());
        Opened += (_, _) => tickTimer.Start();
        Closing += (_, _) => SaveBounds();
        PositionChanged += (_, _) => ScheduleSave();
        SizeChanged += (_, _) => ScheduleSave();
    }

    private TimerOverlayViewModel ViewModel => (TimerOverlayViewModel)DataContext!;

    private void SetClickThrough(bool enabled)
    {
        ViewModel.IsClickThrough = enabled;
        LockButton.IsChecked = enabled;
        if (enabled) lockTimer?.Start();
        else lockTimer?.Stop();
        ClickThrough.Set(this, enabled && !ClickThrough.IsCursorOver(LockButton));
    }

    private void OnRowReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left) ViewModel.EditCommand.Execute(null);
    }

    private void OnEditClicked(object? sender, RoutedEventArgs e) => ViewModel.EditCommand.Execute(null);

    private DispatcherTimer? saveTimer;

    private void ScheduleSave()
    {
        saveTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            saveTimer!.Stop();
            SaveBounds();
        });
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void RestoreBounds()
    {
        if (settings is null) return;
        if (settings.OverlayWidth is double w && settings.OverlayHeight is double h && w > 100 && h > 60)
        {
            Width = w;
            Height = h;
        }
        if (settings.OverlayLeft is double x && settings.OverlayTop is double y)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint((int)x, (int)y);
        }
        else
        {
            // Standard: rechts oben auf dem Hauptbildschirm
            var screen = Screens.Primary;
            if (screen is not null)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new PixelPoint(screen.WorkingArea.Right - (int)(Width * screen.Scaling) - 24, screen.WorkingArea.Y + 120);
            }
        }
    }

    public void SaveBounds()
    {
        if (settings is null || service is null || !IsVisible) return;
        settings.OverlayLeft = Position.X;
        settings.OverlayTop = Position.Y;
        settings.OverlayWidth = Width;
        settings.OverlayHeight = Height;
        service.Save();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Rand-Ziehen zum Skalieren (Fenster hat keine Systemränder)
        var p = e.GetPosition(this);
        const double grip = 8;
        bool right = p.X >= Bounds.Width - grip, bottom = p.Y >= Bounds.Height - grip;
        if (right && bottom) BeginResizeDrag(WindowEdge.SouthEast, e);
        else if (right) BeginResizeDrag(WindowEdge.East, e);
        else if (bottom) BeginResizeDrag(WindowEdge.South, e);
    }
}
