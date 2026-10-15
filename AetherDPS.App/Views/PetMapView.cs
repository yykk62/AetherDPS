using AetherDPS.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AetherDPS.App.Views;

/// <summary>
/// Weltkarte mit Spawnpunkten und eigener Position. Mausrad zoomt, Ziehen verschiebt, Doppelklick zeigt wieder alle Punkte.
/// Kacheln: Assets/maps/&lt;Karte&gt;/&lt;Spalte&gt;_&lt;Zeile&gt;.jpg (512 px, aus tools/update-pet-data.py).
/// </summary>
public sealed class PetMapView : Control
{
    public static readonly StyledProperty<PetMap?> MapProperty = AvaloniaProperty.Register<PetMapView, PetMap?>(nameof(Map));
    public static readonly StyledProperty<IReadOnlyList<PetSpawn>?> SpawnsProperty = AvaloniaProperty.Register<PetMapView, IReadOnlyList<PetSpawn>?>(nameof(Spawns));
    public static readonly StyledProperty<Point?> SelfProperty = AvaloniaProperty.Register<PetMapView, Point?>(nameof(Self));

    /// <summary>&gt; 0: Ausschnitt um die eigene Position mit diesem Radius in Metern (Pet-Radar) statt „alle Punkte“.</summary>
    public static readonly StyledProperty<double> FocusMetersProperty = AvaloniaProperty.Register<PetMapView, double>(nameof(FocusMeters));

    private const int TileSource = 512;
    private static readonly Dictionary<string, Bitmap?[,]> TileCache = [];
    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#11141B"));
    private static readonly IBrush SpawnFill = new SolidColorBrush(Color.Parse("#E0A84A"));
    private static readonly IPen SpawnPen = new Pen(new SolidColorBrush(Color.Parse("#1A1206")), 1.5);
    private static readonly IBrush SelfFill = new SolidColorBrush(Color.Parse("#4FC1E9"));
    private static readonly IPen SelfPen = new Pen(Brushes.White, 2);
    private static readonly IPen SelfRing = new Pen(new SolidColorBrush(Color.Parse("#884FC1E9")), 2);

    private double zoom = 1;          // Bildschirm-Pixel je Karten-Pixel
    private Point center;             // Kartenmitte in Karten-Pixeln
    private Point? dragFrom;
    private bool fitted;

    static PetMapView()
    {
        AffectsRender<PetMapView>(MapProperty, SpawnsProperty, SelfProperty);
        MapProperty.Changed.AddClassHandler<PetMapView>((v, _) => v.fitted = false);
        SpawnsProperty.Changed.AddClassHandler<PetMapView>((v, _) => v.fitted = false);
        SelfProperty.Changed.AddClassHandler<PetMapView>((v, _) =>
        {
            if (v.FocusMeters > 0) v.fitted = false;
        });
        AffectsRender<PetMapView>(FocusMetersProperty);
    }

    public PetMapView()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public PetMap? Map { get => GetValue(MapProperty); set => SetValue(MapProperty, value); }
    public IReadOnlyList<PetSpawn>? Spawns { get => GetValue(SpawnsProperty); set => SetValue(SpawnsProperty, value); }

    /// <summary>Eigene Position in Weltkoordinaten (null = nicht auf dieser Karte).</summary>
    public Point? Self { get => GetValue(SelfProperty); set => SetValue(SelfProperty, value); }

    public double FocusMeters { get => GetValue(FocusMetersProperty); set => SetValue(FocusMetersProperty, value); }

    /// <summary>Größter Radius der Spawnpunkte in Pixeln (Radar: kleiner, weil viele Pets auf einmal).</summary>
    public double MaxDot { get; set; } = 6;

    /// <summary>Ausschnitt so wählen, dass alle Spawnpunkte (und die eigene Position) sichtbar sind.</summary>
    public void Fit()
    {
        ComputeFit();
        InvalidateVisual();
    }

    private void ComputeFit()
    {
        if (Map is not { } map || Bounds.Width < 10 || Bounds.Height < 10) return;
        var points = (Spawns ?? []).Select(s => map.ToPixel(s.X, s.Y)).ToList();
        if (Self is { } self) points.Add(map.ToPixel(self.X, self.Y));
        double minZoom = Math.Min(Bounds.Width, Bounds.Height) / map.Size;
        if (FocusMeters > 0 && Self is { } me)
        {
            var (sx, sy) = map.ToPixel(me.X, me.Y);
            center = new Point(sx, sy);
            zoom = Math.Clamp(Math.Min(Bounds.Width, Bounds.Height) / (2 * FocusMeters * 100 * map.Scale), minZoom, MaxZoom(map));
        }
        else if (points.Count == 0)
        {
            zoom = minZoom;
            center = new Point(map.Size / 2, map.Size / 2);
        }
        else
        {
            double x0 = points.Min(p => p.X), x1 = points.Max(p => p.X), y0 = points.Min(p => p.Y), y1 = points.Max(p => p.Y);
            double pad = map.Size * 0.04;
            double w = x1 - x0 + 2 * pad, h = y1 - y0 + 2 * pad;
            zoom = Math.Clamp(Math.Min(Bounds.Width / w, Bounds.Height / h), minZoom, MaxZoom(map));
            center = new Point((x0 + x1) / 2, (y0 + y1) / 2);
        }
        fitted = true;
    }

    private double MaxZoom(PetMap map) => 3.0 * TileSource / map.Tile;   // höchstens 3-fach über der Kachel-Auflösung

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        fitted = false;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Background, bounds);
        if (Map is not { } map) return;
        if (!fitted) ComputeFit();

        var tiles = Tiles(map);
        double cell = map.Tile * zoom;
        for (int col = 0; col < map.Grid; col++)
            for (int row = 0; row < map.Grid; row++)
            {
                if (tiles[col, row] is not { } tile) continue;
                var rect = new Rect(ToScreen(new Point(col * map.Tile, row * map.Tile)), new Size(cell, cell));
                if (rect.Intersects(bounds)) context.DrawImage(tile, new Rect(tile.Size), rect.Inflate(0.5));
            }

        double r = Math.Clamp(3 + zoom * 40, Math.Min(3.5, MaxDot), MaxDot);
        foreach (var s in Spawns ?? [])
        {
            var (x, y) = map.ToPixel(s.X, s.Y);
            var p = ToScreen(new Point(x, y));
            if (bounds.Inflate(r).Contains(p)) context.DrawEllipse(SpawnFill, SpawnPen, p, r, r);
        }

        if (Self is { } self)
        {
            var (x, y) = map.ToPixel(self.X, self.Y);
            var p = ToScreen(new Point(x, y));
            context.DrawEllipse(null, SelfRing, p, 12, 12);
            context.DrawEllipse(SelfFill, SelfPen, p, 6, 6);
        }
    }

    private Point ToScreen(Point mapPixel) =>
        new((mapPixel.X - center.X) * zoom + Bounds.Width / 2, (mapPixel.Y - center.Y) * zoom + Bounds.Height / 2);

    private Point ToMap(Point screen) =>
        new((screen.X - Bounds.Width / 2) / zoom + center.X, (screen.Y - Bounds.Height / 2) / zoom + center.Y);

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Map is not { } map) return;
        var at = e.GetPosition(this);
        var before = ToMap(at);
        double minZoom = Math.Min(Bounds.Width, Bounds.Height) / map.Size;
        zoom = Math.Clamp(zoom * (e.Delta.Y > 0 ? 1.25 : 0.8), minZoom, MaxZoom(map));
        var after = ToMap(at);
        center = new Point(center.X + before.X - after.X, center.Y + before.Y - after.Y);   // Punkt unter der Maus bleibt stehen
        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>Ziehen wie bei Google Maps: nur mit gedrückter linker Taste, Loslassen beendet es immer.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;   // nicht zusätzlich das Fenster verschieben (Pet-Radar)
        if (e.ClickCount == 2)
        {
            Fit();
            return;
        }
        dragFrom = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (dragFrom is not { } from) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            EndDrag(e.Pointer);   // Loslassen kam nicht an (z. B. außerhalb losgelassen)
            return;
        }
        var now = e.GetPosition(this);
        center = new Point(center.X - (now.X - from.X) / zoom, center.Y - (now.Y - from.Y) / zoom);
        dragFrom = now;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        dragFrom = null;
    }

    private void EndDrag(IPointer pointer)
    {
        dragFrom = null;
        if (pointer.Captured == this) pointer.Capture(null);
    }

    private static Bitmap?[,] Tiles(PetMap map)
    {
        if (TileCache.TryGetValue(map.Key, out var cached)) return cached;
        var tiles = new Bitmap?[map.Grid, map.Grid];
        for (int col = 0; col < map.Grid; col++)
            for (int row = 0; row < map.Grid; row++)
            {
                var uri = new Uri($"avares://AetherDPS/Assets/maps/{map.Key}/{col}_{row}.jpg");
                if (!AssetLoader.Exists(uri)) continue;
                using var stream = AssetLoader.Open(uri);
                tiles[col, row] = new Bitmap(stream);
            }
        return TileCache[map.Key] = tiles;
    }
}
