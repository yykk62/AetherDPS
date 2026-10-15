using AetherDPS.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

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

    private const int TileSource = 512;
    private static readonly Dictionary<string, Bitmap?[,]> TileCache = [];
    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#11141B"));
    private static readonly IBrush SpawnFill = new SolidColorBrush(Color.Parse("#E0A84A"));
    private static readonly IPen SpawnPen = new Pen(new SolidColorBrush(Color.Parse("#1A1206")), 1.5);
    private static readonly IBrush SelfFill = Brushes.White;
    private static readonly IPen SelfPen = new Pen(new SolidColorBrush(Color.Parse("#1E6FD9")), 3);
    private static readonly IPen SelfRing = new Pen(new SolidColorBrush(Color.Parse("#884FC1E9")), 2);

    private double zoom = 1;          // Bildschirm-Pixel je Karten-Pixel
    private Point center;             // Kartenmitte in Karten-Pixeln
    private Point? dragFrom;
    private bool fitted;

    static PetMapView()
    {
        AffectsRender<PetMapView>(MapProperty, SpawnsProperty, SelfProperty);
        MapProperty.Changed.AddClassHandler<PetMapView>((v, _) =>
        {
            v.fitted = false;
            v.following = true;
        });
        // neue Auswahl (andere Punkte) → neu einpassen; gleiche Punkte neu gesetzt (Radar beim Laufen) → nur wenn er folgt
        SpawnsProperty.Changed.AddClassHandler<PetMapView>((v, e) =>
        {
            var before = e.OldValue as IReadOnlyList<PetSpawn>;
            var after = e.NewValue as IReadOnlyList<PetSpawn>;
            bool same = before is not null && after is not null && before.Count == after.Count && (after.Count == 0 || before[0] == after[0]);
            if (!v.AutoFit) return;   // Pet-Finder: Zoom und Ausschnitt bleiben, nur die Punkte ändern sich
            if (v.Minimap)
            {
                if (!same) v.Reveal();
                return;
            }
            if (!same) v.following = true;
            if (v.following) v.fitted = false;
        });
        SelfProperty.Changed.AddClassHandler<PetMapView>((v, e) =>
        {
            if (v.Minimap && e.OldValue is null) v.fitted = false;   // Position erst jetzt bekannt → auf dich einstellen
            else if (v.Minimap) v.FollowSelf();
        });
        AffectsRender<PetMapView>(SpawnBrushesProperty, DotBrushProperty);
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

    /// <summary>Farbe je Monster (Radar: jedes Pet eigene Farbe); fehlt eins, gilt Gold.</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<int, IBrush>?> SpawnBrushesProperty =
        AvaloniaProperty.Register<PetMapView, IReadOnlyDictionary<int, IBrush>?>(nameof(SpawnBrushes));

    public IReadOnlyDictionary<int, IBrush>? SpawnBrushes { get => GetValue(SpawnBrushesProperty); set => SetValue(SpawnBrushesProperty, value); }

    /// <summary>Farbe aller Punkte, wenn keine Farbe je Monster gesetzt ist (Pet-Finder: Farbe der Gattung).</summary>
    public static readonly StyledProperty<IBrush?> DotBrushProperty = AvaloniaProperty.Register<PetMapView, IBrush?>(nameof(DotBrush));

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    /// <summary>Folgt der Ausschnitt der eigenen Position? Aus, sobald man selbst schiebt oder zoomt; ⌖ schaltet es wieder an.</summary>
    private bool following = true;

    /// <summary>
    /// Minimap (Pet-Radar): immer auf dich zentriert und folgt dir, der Zoom bleibt (<see cref="ViewMeters"/>, gemerkt).
    /// Ziehen schaut kurz woanders hin, nach ein paar Sekunden springt die Karte zurück zu dir.
    /// </summary>
    public bool Minimap { get; set; }

    /// <summary>Minimap: sichtbarer Umkreis in Metern (halbe kürzere Seite). Ändert sich nur durch das Mausrad.</summary>
    public double ViewMeters { get; set; } = 600;

    /// <summary>Minimap: Zoom per Mausrad geändert (zum Merken).</summary>
    public event EventHandler? ViewChanged;

    private static readonly TimeSpan BackToSelf = TimeSpan.FromSeconds(5);
    private DispatcherTimer? backTimer;

    private double ZoomFor(PetMap map, double meters) =>
        Math.Clamp(Math.Min(Bounds.Width, Bounds.Height) / (2 * meters * 100 * map.Scale),
            Math.Min(Bounds.Width, Bounds.Height) / map.Size, MaxZoom(map));

    private double MetersFor(PetMap map) => Math.Min(Bounds.Width, Bounds.Height) / (2 * zoom * 100 * map.Scale);

    /// <summary>Minimap: neue Position → Mitte mitziehen, Zoom bleibt.</summary>
    private void FollowSelf()
    {
        if (!following || !fitted || Map is not { } map || Self is not { } me) return;
        var (x, y) = map.ToPixel(me.X, me.Y);
        center = new Point(x, y);
        InvalidateVisual();
    }

    /// <summary>
    /// Minimap: andere Punkte (z. B. ein Pet angeklickt) → eigener Zoom, zentriert auf dich. Liegt kein Punkt im
    /// Ausschnitt, nur so weit herauszoomen, dass der nächste zu sehen ist.
    /// </summary>
    private void Reveal()
    {
        if (!fitted || Map is not { } map || Self is not { } me || Bounds.Width < 10) return;
        following = true;
        var (sx, sy) = map.ToPixel(me.X, me.Y);
        center = new Point(sx, sy);
        zoom = ZoomFor(map, ViewMeters);
        double half = Math.Min(Bounds.Width, Bounds.Height) / 2 / zoom;
        var nearest = (Spawns ?? []).Select(s => map.ToPixel(s.X, s.Y))
            .Select(p => Math.Max(Math.Abs(p.X - sx), Math.Abs(p.Y - sy))).DefaultIfEmpty(0).Min();
        if (nearest > half * 0.9)
            zoom = Math.Clamp(Math.Min(Bounds.Width, Bounds.Height) / 2 / (nearest * 1.2), Math.Min(Bounds.Width, Bounds.Height) / map.Size, MaxZoom(map));
        InvalidateVisual();
    }

    /// <summary>⌖: Ausschnitt auf die eigene Position (wie bei Google Maps), danach folgt er wieder.</summary>
    public void CenterOnSelf()
    {
        following = true;
        backTimer?.Stop();
        if (Map is not { } map || Self is not { } me) return;
        if (Minimap)
        {
            var (mx, my) = map.ToPixel(me.X, me.Y);
            center = new Point(mx, my);
            zoom = ZoomFor(map, ViewMeters);
            fitted = true;
            InvalidateVisual();
            return;
        }
        var (x, y) = map.ToPixel(me.X, me.Y);
        center = new Point(x, y);
        double close = Math.Min(Bounds.Width, Bounds.Height) / (2 * 800 * 100 * map.Scale);   // ~800 m Umkreis
        if (zoom < close) zoom = Math.Min(close, MaxZoom(map));
        InvalidateVisual();
    }

    /// <summary>
    /// Ausschnitt an die Punkte anpassen? Aus (Pet-Finder): immer die ganze Karte bzw. der Ausschnitt, den man selbst
    /// gewählt hat – ein anderes Pet setzt nur die Markierungen.
    /// </summary>
    public bool AutoFit { get; set; } = true;

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
        if (Minimap && Self is { } at)
        {
            var (ax, ay) = map.ToPixel(at.X, at.Y);
            center = new Point(ax, ay);
            zoom = ZoomFor(map, ViewMeters);
            following = true;
        }
        else if (points.Count == 0 || !AutoFit)
        {
            // ganze Karte = alle vorhandenen Kacheln (das Meer drumherum weglassen)
            var tiles = Tiles(map);
            int c0 = int.MaxValue, c1 = -1, r0 = int.MaxValue, r1 = -1;
            for (int col = 0; col < map.Grid; col++)
                for (int row = 0; row < map.Grid; row++)
                    if (tiles[col, row] is not null)
                    {
                        c0 = Math.Min(c0, col); c1 = Math.Max(c1, col);
                        r0 = Math.Min(r0, row); r1 = Math.Max(r1, row);
                    }
            if (c1 < 0)
            {
                zoom = minZoom;
                center = new Point(map.Size / 2, map.Size / 2);
            }
            else
            {
                double w = (c1 - c0 + 1) * map.Tile, h = (r1 - r0 + 1) * map.Tile;
                zoom = Math.Clamp(Math.Min(Bounds.Width / w, Bounds.Height / h), minZoom, MaxZoom(map));
                center = new Point((c0 * map.Tile + (c1 + 1) * map.Tile) / 2.0, (r0 * map.Tile + (r1 + 1) * map.Tile) / 2.0);
            }
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

    /// <summary>Aktueller Ausschnitt (für Tests): Zoom und Mitte in Karten-Pixeln.</summary>
    public (double Zoom, Point Center) View => (zoom, center);

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
            var fill = SpawnBrushes is { } brushes && brushes.TryGetValue(s.Npc, out var b) ? b : DotBrush ?? SpawnFill;
            if (bounds.Inflate(r).Contains(p)) context.DrawEllipse(fill, SpawnPen, p, r, r);
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
        if (Minimap)
        {
            // Minimap: Zoom um dich (folgt sie gerade) bzw. um die Maus, und merken
            if (!following)
            {
                var moved = ToMap(at);
                center = new Point(center.X + before.X - moved.X, center.Y + before.Y - moved.Y);
            }
            ViewMeters = MetersFor(map);
            ViewChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        var after = ToMap(at);
        center = new Point(center.X + before.X - after.X, center.Y + before.Y - after.Y);   // Punkt unter der Maus bleibt stehen
        following = false;
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
            if (Minimap)
            {
                CenterOnSelf();
                return;
            }
            following = true;
            Fit();
            return;
        }
        dragFrom = e.GetPosition(this);
        backTimer?.Stop();
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
        following = false;
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
        bool dragged = dragFrom is not null && !following;
        dragFrom = null;
        if (pointer.Captured == this) pointer.Capture(null);
        if (Minimap && dragged)
        {
            // kurz woanders hingeschaut → gleich wieder zu dir
            backTimer ??= new DispatcherTimer(BackToSelf, DispatcherPriority.Background, (_, _) => CenterOnSelf());
            backTimer.Stop();
            backTimer.Start();
        }
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
