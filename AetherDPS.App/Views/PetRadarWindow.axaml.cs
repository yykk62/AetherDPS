using AetherDPS.App.ViewModels;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AetherDPS.App.Views;

/// <summary>Pet-Radar am Bildschirmrand, solange die Spielkarte offen ist. Ziehen verschiebt, Position wird gemerkt.</summary>
public partial class PetRadarWindow : Window
{
    private readonly MeterSettings? settings;

    public PetRadarWindow() => InitializeComponent();

    public PetRadarWindow(PetRadarViewModel vm, MeterSettings settings) : this()
    {
        DataContext = vm;
        this.settings = settings;
        WindowStartupLocation = WindowStartupLocation.Manual;
        // Minimap: Zoom vom letzten Mal
        RadarMap.ViewMeters = settings.PetRadarMeters is > 20 and < 100_000 and var meters ? meters : PetRadarViewModel.RadiusMeters;
        RadarMap.ViewChanged += (_, _) =>
        {
            settings.PetRadarMeters = Math.Round(RadarMap.ViewMeters);
            settings.Save();
        };
        EventHandler show = (_, _) =>
        {
            Place();
            if (!IsVisible) Show();
        };
        EventHandler hide = (_, _) =>
        {
            Remember();
            Hide();
        };
        vm.ShowRequested += show;
        vm.HideRequested += hide;
        Closed += (_, _) =>
        {
            vm.ShowRequested -= show;
            vm.HideRequested -= hide;
        };
        Root.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            BeginMoveDrag(e);
            Remember();
        };
    }

    /// <summary>Gemerkte Stelle, sonst links, etwas über der Mitte (die Spielkarte liegt in der Mitte).</summary>
    private void Place()
    {
        if (settings?.PetRadarLeft is double x && settings.PetRadarTop is double y)
        {
            Position = new PixelPoint((int)x, (int)y);
            return;
        }
        if ((Screens.Primary ?? Screens.All.FirstOrDefault()) is { } screen)
            Position = new PixelPoint(screen.WorkingArea.X + 24, screen.WorkingArea.Y + screen.WorkingArea.Height / 4);
    }

    private void Remember()
    {
        if (settings is null || !IsVisible) return;
        settings.PetRadarLeft = Position.X;
        settings.PetRadarTop = Position.Y;
        settings.Save();
    }

    private void OnCenter(object? sender, RoutedEventArgs e) => RadarMap.CenterOnSelf();

    private void OnClose(object? sender, RoutedEventArgs e) => (DataContext as PetRadarViewModel)?.Dismiss();
}
