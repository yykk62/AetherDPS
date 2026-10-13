using Aion2Meter.App.Services;
using Aion2Meter.App.ViewModels;
using Aion2Meter.Engine;
using AionDpsMeter.Services.Services.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;

namespace Aion2Meter.App.Views;

public partial class MainWindow : Window
{
    private readonly IAppSettingsService? settings;

    public MainWindow() => InitializeComponent();

    public MainWindow(MainViewModel viewModel, IAppSettingsService settings, TrackingMode mode) : this()
    {
        DataContext = viewModel;
        this.settings = settings;
        RestoreBounds();
        ApplyOpacity();
        settings.SettingsChanged += (_, _) => Dispatcher.UIThread.Post(ApplyOpacity);

        Header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        CloseButton.Click += (_, _) =>
        {
            if (mode.IsActive) mode.Standby();   // App blendet dann aus
            else Hide();
        };
        ClickThroughButton.IsCheckedChanged += (_, _) => SetClickThrough(ClickThroughButton.IsChecked == true);
        lockTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(60), DispatcherPriority.Input, (_, _) => UpdateLockHover());
        Closing += (_, _) => SaveBounds();
    }

    private readonly DispatcherTimer? lockTimer;

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    public bool IsClickThrough { get; private set; }

    /// <summary>
    /// Gesperrt = Klicks gehen ins Spiel. Nur das Schloss bleibt klickbar: Ein Timer
    /// schaut, ob die Maus darüber ist, und macht das Fenster solange wieder klickbar.
    /// </summary>
    public void SetClickThrough(bool enabled)
    {
        IsClickThrough = enabled;
        ClickThroughButton.IsChecked = enabled;
        ViewModel.IsClickThrough = enabled;
        if (enabled) lockTimer?.Start();
        else lockTimer?.Stop();
        ClickThrough.Set(this, enabled && !ClickThrough.IsCursorOver(ClickThroughButton));
    }

    private void UpdateLockHover()
    {
        if (!IsClickThrough) return;
        ClickThrough.Set(this, !ClickThrough.IsCursorOver(ClickThroughButton));
    }

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: PlayerRow row })
            ViewModel.SelectPlayerCommand.Execute(row);
    }

    private void ApplyOpacity()
    {
        // Nur der Hintergrund wird durchsichtig – Text und Balken bleiben voll lesbar.
        byte alpha = (byte)Math.Round(Math.Clamp(settings?.WindowOpacity ?? 0.9, 0.2, 1.0) * 255);
        Root.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(alpha, 0x14, 0x18, 0x20));
    }

    private void RestoreBounds()
    {
        if (settings is null) return;
        if (settings.WindowWidth is double w && settings.WindowHeight is double h && w > 100 && h > 80)
        {
            Width = w;
            Height = h;
        }
        if (settings.WindowLeft is double x && settings.WindowTop is double y)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint((int)x, (int)y);
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public void SaveBounds()
    {
        if (settings is null) return;
        settings.WindowLeft = Position.X;
        settings.WindowTop = Position.Y;
        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
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
