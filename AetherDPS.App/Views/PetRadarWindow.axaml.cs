using AetherDPS.App.Services;
using AetherDPS.App.ViewModels;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AetherDPS.App.Views;

/// <summary>
/// Pet-Radar über dem Spiel. Beim ersten Mal mittig und so groß wie die Spielkarte (es kann sie ersetzen).
/// Alle Knöpfe liegen klein auf der Karte: ⠿ startet den Platzierungsmodus (dort wird verschoben), die Ecke unten
/// rechts ändert die Größe, ▾ klappt es zu einer kleinen Leiste ein, ☰ blendet die Liste ein/aus.
/// Position, Größe und Zustand werden gemerkt.
/// </summary>
public partial class PetRadarWindow : Window, IPlaceableOverlay
{
    private const double SmallestWidth = 240, SmallestMap = 160;
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
            if (placing)
            {
                visibleBeforePlacement = false;   // nach dem Platzieren zu
                return;
            }
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
        MoveHandle.PointerPressed += OnMoveHandlePressed;   // auf der Karte selbst verschiebt Ziehen den Ausschnitt
        ClickThrough.NeverActivate(this);
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
        if (settings?.PetRadarWidth is double w && settings.PetRadarMapHeight is double h) return (Math.Max(SmallestWidth, w), Math.Max(SmallestMap, h));
        if ((Screens.Primary ?? Screens.All.FirstOrDefault()) is { } screen)
        {
            double map = screen.WorkingArea.Height / screen.Scaling * 0.5;
            return (Math.Round(map * 1.25), Math.Round(map));
        }
        return (290, 230);
    }

    /// <summary>
    /// Gemerkte Stelle, sonst mittig über der Spielkarte. Eine Stelle aus der Zeit vor der Größenänderung (kleines Radar
    /// am Rand) gilt nicht – sonst ragte das große Radar aus dem Bild. Immer ganz auf dem Bildschirm.
    /// </summary>
    private void Place()
    {
        if ((Screens.Primary ?? Screens.All.FirstOrDefault()) is not { } primary) return;
        var (width, map) = Size();
        double height = map + 220;   // Liste
        bool sized = settings?.PetRadarWidth is not null;
        if (sized && settings!.PetRadarLeft is double x && settings.PetRadarTop is double y)
        {
            var at = new PixelPoint((int)x, (int)y);
            var screen = Screens.ScreenFromPoint(at) ?? primary;
            var area = screen.WorkingArea;
            double scale = screen.Scaling;
            int w = (int)(width * scale), h = (int)(Math.Min(height, area.Height / scale) * scale);
            Position = new PixelPoint(Math.Clamp(at.X, area.X, Math.Max(area.X, area.Right - w)), Math.Clamp(at.Y, area.Y, Math.Max(area.Y, area.Bottom - h)));
            return;
        }
        var wa = primary.WorkingArea;
        double s = primary.Scaling;
        Position = new PixelPoint(wa.X + (int)((wa.Width - width * s) / 2), wa.Y + (int)Math.Max(0, (wa.Height - height * s) / 2));
    }

    private void Remember()
    {
        if (settings is null || !IsVisible) return;
        settings.PetRadarLeft = Position.X;
        settings.PetRadarTop = Position.Y;
        if (!IsCollapsed)
        {
            settings.PetRadarWidth ??= Math.Round(Width);
            settings.PetRadarMapHeight ??= Math.Round(RadarMap.Height);
        }
        settings.Save();
    }

    /// <summary>Eingeklappt ist ein Klick „aufklappen“. Verschoben wird im Platzierungsmodus.</summary>
    private void OnRootPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || resizeFrom is not null) return;
        if (IsCollapsed) SetCollapsed(false);
    }

    /// <summary>⠿: alle Overlays platzieren (statt nur dieses Fenster zu ziehen).</summary>
    private void OnMoveHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        PlacementRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? PlacementRequested;

    private bool placing, visibleBeforePlacement;

    public string? PlacementLabel => Loc.T("radar.title");

    public IEnumerable<Window> PlacementWindows => [this];

    /// <summary>In der gemerkten (bzw. Start-)Größe zeigen – so groß, wie es im Spiel steht.</summary>
    public void BeginPlacement()
    {
        placing = true;
        visibleBeforePlacement = IsVisible;
        if (!IsVisible)
        {
            (DataContext as PetRadarViewModel)?.ShowSample(true);   // offen bleiben die echten Daten
            Place();
            Show();
        }
    }

    public void EndPlacement()
    {
        placing = false;
        if (settings is not null)
        {
            settings.PetRadarLeft = Position.X;
            settings.PetRadarTop = Position.Y;
            if (!IsCollapsed)
            {
                settings.PetRadarWidth ??= Math.Round(Width);
                settings.PetRadarMapHeight ??= Math.Round(RadarMap.Height);
            }
            settings.Save();
        }
        (DataContext as PetRadarViewModel)?.ShowSample(false);
        if (!visibleBeforePlacement) Hide();
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
        Width = Math.Max(SmallestWidth, resizeWidth + (now.X - from.X) / scale);
        RadarMap.Height = Math.Max(SmallestMap, resizeMap + (now.Y - from.Y) / scale);
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
