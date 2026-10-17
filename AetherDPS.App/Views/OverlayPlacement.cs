using AetherDPS.App.Services;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace AetherDPS.App.Views;

/// <summary>Ein Overlay, das im gemeinsamen Platzierungsmodus mitmacht.</summary>
public interface IPlaceableOverlay
{
    /// <summary>Name am Rahmen (null = keiner, z. B. Timer-Pills: zu klein, sie erklären sich selbst).</summary>
    string? PlacementLabel { get; }

    /// <summary>Beispieldaten an, zeigen, klickbar machen.</summary>
    void BeginPlacement();

    /// <summary>Position speichern, echte Daten und Sperre zurück, Sichtbarkeit wie vorher.</summary>
    void EndPlacement();

    IEnumerable<Window> PlacementWindows { get; }
}

/// <summary>
/// Platzierungsmodus für alle Overlays (docs/DESIGN.md, „Eine Schicht für alle Overlays“): Ein Kürzel zeigt Kampfanzeige,
/// Timer, Pet-Radar und Stagger-Warnung gleichzeitig mit Beispieldaten in echter Größe. Ziehen verschiebt, die Fenster
/// rasten am Bildschirmrand und aneinander ein; Esc, Enter oder „Fertig“ legt sie fest und speichert.
/// Die Overlays nehmen dabei nie den Fokus – die Tasten laufen über <see cref="GameHotkey"/>.
/// </summary>
public sealed class OverlayPlacement
{
    private const int SnapDistance = 12;

    private readonly Func<IEnumerable<IPlaceableOverlay>> overlays;
    private readonly Dictionary<Window, Cursor?> attached = [];
    private List<IPlaceableOverlay> active = [];
    private PlacementBar? bar;
    private Window? dragging;
    private PixelPoint dragFrom, windowFrom;

    public OverlayPlacement(Func<IEnumerable<IPlaceableOverlay>> overlays) => this.overlays = overlays;

    public bool IsActive { get; private set; }

    /// <summary>Festgelegt – die App stellt danach die normale Sichtbarkeit wieder her.</summary>
    public event EventHandler? Ended;

    /// <summary>Alle Fenster, die gerade verschoben werden können.</summary>
    public IReadOnlyCollection<Window> Windows => attached.Keys;

    public PlacementBar? Bar => bar;

    public void Toggle()
    {
        if (IsActive) End();
        else Begin();
    }

    public void Begin()
    {
        if (IsActive) return;
        IsActive = true;
        GameHotkey.PlacementActive = true;
        active = overlays().ToList();
        foreach (var overlay in active) overlay.BeginPlacement();
        Refresh();
        if (bar is null)
        {
            bar = new PlacementBar();
            bar.Done += (_, _) => End();
        }
        bar.ShowAtTop();
    }

    /// <summary>Fenster neu einsammeln (Timer-Gruppen entstehen und verschwinden beim Gruppieren).</summary>
    public void Refresh()
    {
        if (!IsActive) return;
        var now = active.SelectMany(o => o.PlacementWindows.Select(w => (Window: w, Overlay: o))).ToList();
        foreach (var gone in attached.Keys.Except(now.Select(n => n.Window)).ToList()) Detach(gone);
        foreach (var (window, overlay) in now)
            if (!attached.ContainsKey(window)) Attach(window, overlay.PlacementLabel);
    }

    public void End()
    {
        if (!IsActive) return;
        EndDrag();
        foreach (var window in attached.Keys.ToList()) Detach(window);
        IsActive = false;   // vor EndPlacement: die Timer bauen dabei Fenster um und dürfen nicht wieder angehängt werden
        GameHotkey.PlacementActive = false;
        foreach (var overlay in active) overlay.EndPlacement();
        active = [];
        bar?.Hide();
        Ended?.Invoke(this, EventArgs.Empty);
    }

    private void Attach(Window window, string? label)
    {
        attached[window] = window.Cursor;
        window.Cursor = new Cursor(StandardCursorType.SizeAll);
        // Tunnel: vor den Knöpfen, der Karte und dem Größen-Griff – im Modus wird nur verschoben
        window.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        window.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        window.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        window.PointerCaptureLost += OnCaptureLost;
        if (window.Content is Visual root) AdornerLayer.SetAdorner(root, Frame(label, (root as Border)?.CornerRadius));
    }

    private void Detach(Window window)
    {
        if (!attached.Remove(window, out var cursor)) return;
        window.Cursor = cursor;
        window.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        window.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        window.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        window.PointerCaptureLost -= OnCaptureLost;
        if (window.Content is Visual root) AdornerLayer.SetAdorner(root, null);
        if (dragging == window) dragging = null;
    }

    /// <summary>Rahmen in Äther-Violett (Fokus/„in Arbeit“) mit dem Namen des Overlays oben links.</summary>
    private static Control Frame(string? label, CornerRadius? radius)
    {
        var frame = new Border
        {
            Classes = { "placing" },
            BorderBrush = TokenBrush.Get("Accent.Phase"),
            Background = TokenBrush.Get("Surface.Placing"),
            BorderThickness = new Thickness(2),
            CornerRadius = radius ?? new CornerRadius(8),
            IsHitTestVisible = false,
        };
        if (label is not null)
            frame.Child = new Border
            {
                Background = TokenBrush.Get("Surface.WellDeep"),
                BorderBrush = TokenBrush.Get("Accent.Phase"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2),
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = label,
                    Foreground = TokenBrush.Get("Accent.Phase"),
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    FontFamily = Application.Current?.TryGetResource("Font.Ui", null, out var f) == true && f is FontFamily font ? font : FontFamily.Default,
                },
            };
        return frame;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Window window) return;
        if (!e.GetCurrentPoint(window).Properties.IsLeftButtonPressed) return;   // Rechtsklick: Menü der Pills bleibt
        e.Handled = true;
        dragging = window;
        dragFrom = window.PointToScreen(e.GetPosition(window));
        windowFrom = window.Position;
        e.Pointer.Capture(window);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Window window || dragging != window) return;
        e.Handled = true;
        var now = window.PointToScreen(e.GetPosition(window));
        MoveTo(window, windowFrom + (now - dragFrom));
    }

    /// <summary>Fenster an eine Stelle setzen und dabei einrasten lassen (auch für Tests).</summary>
    public void MoveTo(Window window, PixelPoint wanted)
    {
        double scaling = window.RenderScaling;
        var size = PixelSize.FromSize(window.ClientSize, scaling);
        var moving = new ScreenRect(wanted.X, wanted.Y, size.Width, size.Height);
        var others = attached.Keys.Where(w => w != window && w.IsVisible).Select(RectOf);
        var screen = window.Screens.ScreenFromPoint(wanted + new PixelPoint(size.Width / 2, size.Height / 2)) ?? window.Screens.Primary;
        var areas = screen is null ? [] : new[] { ToRect(screen.Bounds), ToRect(screen.WorkingArea) };
        var (x, y) = OverlaySnap.Snap(moving, others, areas, (int)(SnapDistance * scaling), (int)(TimerPill.Gap * scaling));
        window.Position = new PixelPoint(x, y);
    }

    private static ScreenRect RectOf(Window w)
    {
        var size = PixelSize.FromSize(w.ClientSize, w.RenderScaling);
        return new ScreenRect(w.Position.X, w.Position.Y, size.Width, size.Height);
    }

    private static ScreenRect ToRect(PixelRect r) => new(r.X, r.Y, r.Width, r.Height);

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (dragging is null) return;
        e.Handled = true;
        if (e.Pointer.Captured is not null) e.Pointer.Capture(null);
        dragging = null;
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e) => dragging = null;

    private void EndDrag() => dragging = null;
}
