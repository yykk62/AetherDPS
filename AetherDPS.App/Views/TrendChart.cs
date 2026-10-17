using System.Globalization;
using AetherDPS.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AetherDPS.App.Views;

/// <summary>
/// Verlaufskurve über die Zeit (z. B. Kampfkraft): Linie mit kleinen Punkten je Messung, der jüngste Punkt als
/// Raute, rechts Höchst- und Tiefstwert (die Achse beginnt nicht bei null, deshalb keine Fläche), unten das erste
/// Datum und „heute“. Ein einzelner Punkt erscheint als Raute ohne Linie.
/// Farben aus den Tokens (docs/DESIGN.md).
/// </summary>
public sealed class TrendChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<TrendPoint>?> PointsProperty =
        AvaloniaProperty.Register<TrendChart, IReadOnlyList<TrendPoint>?>(nameof(Points));
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<TrendChart, IBrush?>(nameof(LineBrush));
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<TrendChart, IBrush?>(nameof(GridBrush));
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<TrendChart, IBrush?>(nameof(LabelBrush));
    public static readonly StyledProperty<string?> EndLabelProperty =
        AvaloniaProperty.Register<TrendChart, string?>(nameof(EndLabel));
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<TrendChart>();

    static TrendChart()
    {
        AffectsRender<TrendChart>(PointsProperty, LineBrushProperty, GridBrushProperty, LabelBrushProperty, EndLabelProperty);
    }

    public IReadOnlyList<TrendPoint>? Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    /// <summary>Beschriftung rechts unten (z. B. „heute“).</summary>
    public string? EndLabel { get => GetValue(EndLabelProperty); set => SetValue(EndLabelProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public static readonly StyledProperty<Func<double, string>?> FormatProperty =
        AvaloniaProperty.Register<TrendChart, Func<double, string>?>(nameof(Format));
    /// <summary>Wertformat für Höchst- und Tiefstwert rechts (ohne: keine Beschriftung).</summary>
    public Func<double, string>? Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    private const double LabelRoom = 18, Pad = 6, ScaleRoom = 56;

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 90 : availableSize.Height);

    public override void Render(DrawingContext context)
    {
        var points = Points ?? [];
        double full = Bounds.Width, w = full - (Format is null ? 0 : ScaleRoom), h = Bounds.Height - LabelRoom;
        if (w <= 2 * Pad || h <= 2 * Pad || points.Count == 0) return;

        var grid = new Pen(GridBrush, 1);
        context.DrawLine(grid, new Point(0, h + 0.5), new Point(w, h + 0.5));

        DateTime t0 = points[0].At, t1 = points[^1].At;
        double v0 = points.Min(p => p.Value), v1 = points.Max(p => p.Value);
        double span = Math.Max(v1 - v0, Math.Max(1, Math.Abs(v1) * 0.01));
        double lo = v0 - span * 0.15, hi = v1 + span * 0.15;
        double seconds = Math.Max(1, (t1 - t0).TotalSeconds);
        Point P(TrendPoint p) => new(
            points.Count == 1 ? w - Pad : Pad + (p.At - t0).TotalSeconds / seconds * (w - 2 * Pad),
            Pad + (1 - (p.Value - lo) / (hi - lo)) * (h - 2 * Pad));

        if (points.Count > 1)
        {
            var line = new StreamGeometry();
            using (var g = line.Open())
            {
                g.BeginFigure(P(points[0]), false);
                foreach (var p in points.Skip(1)) g.LineTo(P(p));
                g.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(LineBrush, 2, lineJoin: PenLineJoin.Round), line);
            foreach (var p in points.Take(points.Count - 1)) context.DrawEllipse(LineBrush, null, P(p), 2.2, 2.2);
        }

        // jüngster Punkt als Raute
        var last = P(points[^1]);
        var diamond = new StreamGeometry();
        using (var g = diamond.Open())
        {
            g.BeginFigure(new Point(last.X, last.Y - 5), true);
            g.LineTo(new Point(last.X + 5, last.Y));
            g.LineTo(new Point(last.X, last.Y + 5));
            g.LineTo(new Point(last.X - 5, last.Y));
            g.EndFigure(true);
        }
        context.DrawGeometry(LineBrush, null, diamond);

        // erstes Datum links, Endbeschriftung rechts
        var culture = CultureInfo.CurrentUICulture;
        var face = new Typeface(FontFamily);
        var left = new FormattedText(t0.ToString("d", culture), culture, FlowDirection.LeftToRight, face, 11, LabelBrush);
        context.DrawText(left, new Point(0, h + 4));
        if (EndLabel is { Length: > 0 } end)
        {
            var right = new FormattedText(end, culture, FlowDirection.LeftToRight, face, 11, LabelBrush);
            context.DrawText(right, new Point(w - right.Width, h + 4));
        }
        // Höchst- und Tiefstwert rechts neben der Kurve, auf ihrer Höhe
        if (Format is { } format && points.Count > 1)
        {
            var max = new FormattedText(format(v1), culture, FlowDirection.LeftToRight, face, 11, LabelBrush);
            var min = new FormattedText(format(v0), culture, FlowDirection.LeftToRight, face, 11, LabelBrush);
            double yMax = Pad + (1 - (v1 - lo) / (hi - lo)) * (h - 2 * Pad), yMin = Pad + (1 - (v0 - lo) / (hi - lo)) * (h - 2 * Pad);
            context.DrawText(max, new Point(full - max.Width, yMax - max.Height / 2));
            if (yMin - yMax > max.Height) context.DrawText(min, new Point(full - min.Width, yMin - min.Height / 2));
        }
    }
}
