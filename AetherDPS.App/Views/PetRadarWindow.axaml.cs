using AetherDPS.App.ViewModels;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AetherDPS.App.Views;

/// <summary>
/// Pet-Radar über dem Spiel. Beim ersten Mal mittig und so groß wie die Spielkarte (es kann sie ersetzen).
/// Alle Knöpfe liegen klein auf der Karte: ⠿ (oder der Rand) verschiebt, die Ecke unten rechts ändert die Größe,
/// ▾ klappt es zu einer kleinen Leiste ein, ☰ blendet die Liste ein/aus.
/// Position, Größe und Zustand werden gemerkt.
/// </summary>
public partial class PetRadarWindow : Window
{
    private const double MinWidth = 240, MinMap = 160;
    private readonly MeterSettings? settings;
    private Point? resizeFrom;
    private double resizeWidth, resizeMap;

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
        Root.PointerPressed += OnRootPressed;
        MoveHandle.PointerPressed += OnRootPressed;   // auf der Karte selbst verschiebt Ziehen den Ausschnitt
        Grip.PointerPressed += OnGripPressed;
        Grip.PointerMoved += OnGripMoved;
        Grip.PointerReleased += (_, e) => EndResize(e.Pointer);
        Grip.PointerCaptureLost += (_, _) => resizeFrom = null;
        ApplyLayout();
    }

    private bool IsCollapsed => settings?.PetRadarCollapsed == true;

    /// <summary>Eingeklappt: nur die Leiste (Breite nach Inhalt); offen: gemerkte bzw. Start-Größe.</summary>
    private void ApplyLayout()
    {
        MiniBar.IsVisible = IsCollapsed;
        Full.IsVisible = !IsCollapsed;
        Grip.IsVisible = !IsCollapsed;
        if (IsCollapsed)
        {
            SizeToContent = SizeToContent.WidthAndHeight;
            return;
        }
        SizeToContent = SizeToContent.Height;
        var (width, map) = Size();
        Width = width;
        RadarMap.Height = map;
    }

    /// <summary>Gemerkte Größe, sonst so groß wie die Spielkarte: Karte ~ halbe Bildschirmhöhe, etwas breiter als hoch.</summary>
    private (double Width, double Map) Size()
    {
        if (settings?.PetRadarWidth is double w && settings.PetRadarMapHeight is double h) return (Math.Max(MinWidth, w), Math.Max(MinMap, h));
        if ((Screens.Primary ?? Screens.All.FirstOrDefault()) is { } screen)
        {
            double map = screen.WorkingArea.Height / screen.Scaling * 0.5;
            return (Math.Round(map * 1.25), Math.Round(map));
        }
        return (290, 230);
    }

    /// <summary>Gemerkte Stelle, sonst mittig über der Spielkarte.</summary>
    private void Place()
    {
        if (settings?.PetRadarLeft is double x && settings.PetRadarTop is double y)
        {
            Position = new PixelPoint((int)x, (int)y);
            return;
        }
        if ((Screens.Primary ?? Screens.All.FirstOrDefault()) is not { } screen) return;
        var (width, map) = Size();
        var area = screen.WorkingArea;
        double scale = screen.Scaling;
        double height = map + 260;   // Kopf, Liste
        Position = new PixelPoint(area.X + (int)((area.Width - width * scale) / 2), area.Y + (int)Math.Max(0, (area.Height - height * scale) / 2));
    }

    private void Remember()
    {
        if (settings is null || !IsVisible) return;
        settings.PetRadarLeft = Position.X;
        settings.PetRadarTop = Position.Y;
        settings.Save();
    }

    /// <summary>Ziehen verschiebt; eingeklappt ist ein Klick ohne Bewegung „aufklappen“.</summary>
    private void OnRootPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || resizeFrom is not null) return;
        var before = Position;
        BeginMoveDrag(e);   // unter Windows kehrt das erst nach dem Loslassen zurück
        if (IsCollapsed && Position == before)
        {
            SetCollapsed(false);
            return;
        }
        Remember();
    }

    private void OnCollapse(object? sender, RoutedEventArgs e) => SetCollapsed(true);

    public void SetCollapsed(bool collapsed)
    {
        if (settings is null) return;
        settings.PetRadarCollapsed = collapsed;
        settings.Save();
        ApplyLayout();
    }

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;   // nicht verschieben
        resizeFrom = this.PointToScreen(e.GetPosition(this)).ToPoint(1);
        resizeWidth = Width;
        resizeMap = RadarMap.Height;
        e.Pointer.Capture(Grip);
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (resizeFrom is not { } from) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            EndResize(e.Pointer);
            return;
        }
        var now = this.PointToScreen(e.GetPosition(this)).ToPoint(1);
        double scale = RenderScaling;
        Width = Math.Max(MinWidth, resizeWidth + (now.X - from.X) / scale);
        RadarMap.Height = Math.Max(MinMap, resizeMap + (now.Y - from.Y) / scale);
    }

    private void EndResize(IPointer pointer)
    {
        if (resizeFrom is null) return;
        resizeFrom = null;
        if (pointer.Captured == Grip) pointer.Capture(null);
        if (settings is null) return;
        settings.PetRadarWidth = Math.Round(Width);
        settings.PetRadarMapHeight = Math.Round(RadarMap.Height);
        settings.Save();
    }

    private void OnCenter(object? sender, RoutedEventArgs e) => RadarMap.CenterOnSelf();

    /// <summary>☰: Liste der fehlenden Pets ein/aus. Zu = ganz weg; ein gewähltes Pet wird dabei abgewählt.</summary>
    private void OnToggleList(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PetRadarViewModel vm) return;
        vm.ListOpen = !vm.ListOpen;
        if (!vm.ListOpen && vm.SelectedPet is not null) vm.ShowRadarCommand.Execute(null);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => (DataContext as PetRadarViewModel)?.Dismiss();
}
