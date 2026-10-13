using Aion2Meter.App.Services;
using Aion2Meter.App.ViewModels;
using AionDpsMeter.Services.Services.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Aion2Meter.App.Views;

public partial class MainWindow : Window
{
    private readonly IAppSettingsService? settings;

    public MainWindow() => InitializeComponent();

    public MainWindow(MainViewModel viewModel, IAppSettingsService settings) : this()
    {
        DataContext = viewModel;
        this.settings = settings;
        RestoreBounds();

        Header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        CloseButton.Click += (_, _) => (Application.Current as App)?.Quit();
        ClickThroughButton.IsCheckedChanged += (_, _) =>
        {
            if (ClickThroughButton.IsChecked == true) SetClickThrough(true);
        };
        Closing += (_, _) => SaveBounds();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    public bool IsClickThrough { get; private set; }

    public void SetClickThrough(bool enabled)
    {
        IsClickThrough = enabled;
        ClickThrough.Set(this, enabled);
        ClickThroughButton.IsChecked = enabled;
        ViewModel.IsClickThrough = enabled;
    }

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: PlayerRow row })
            ViewModel.SelectPlayerCommand.Execute(row);
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
