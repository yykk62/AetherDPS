using AetherDPS.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AetherDPS.App.Views;

/// <summary>Ping-Verlauf der letzten Minuten als Linie, Spitzen rot markiert.</summary>
public sealed class PingGraph : Control
{
    public static readonly StyledProperty<IReadOnlyList<PingSample>?> SamplesProperty =
        AvaloniaProperty.Register<PingGraph, IReadOnlyList<PingSample>?>(nameof(Samples));

    public static readonly StyledProperty<double> SpikeThresholdProperty =
        AvaloniaProperty.Register<PingGraph, double>(nameof(SpikeThreshold), double.MaxValue);

    static PingGraph() => AffectsRender<PingGraph>(SamplesProperty, SpikeThresholdProperty);

    public IReadOnlyList<PingSample>? Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    public double SpikeThreshold
    {
        get => GetValue(SpikeThresholdProperty);
        set => SetValue(SpikeThresholdProperty, value);
    }

    /// <summary>Angezeigtes Zeitfenster.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(5);

    private static IPen GridPen => new Pen(Services.TokenBrush.Get("Line.Well"), 1);
    private static IPen LinePen => new Pen(Services.TokenBrush.Get("Text.Primary"), 1.6);
    private static IBrush SpikeBrush => Services.TokenBrush.Get("Signal.Bad");
    private static IBrush LabelBrush => Services.TokenBrush.Get("Text.Muted");
    private static readonly Typeface Font = new("Inter");

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        double left = 34, bottom = bounds.Height - 16, top = 6, right = bounds.Width - 6;
        if (right <= left || bottom <= top) return;

        var now = DateTime.Now;
        var from = now - Window;
        var points = (Samples ?? []).Where(s => s.At >= from).ToList();

        // Skala: mindestens 40 ms, sonst bis knapp über den Höchstwert
        double maxMs = Math.Max(40, points.Count > 0 ? points.Max(p => p.Ms) * 1.2 : 40);
        maxMs = Math.Ceiling(maxMs / 20) * 20;   // glatte Achsenwerte (Viertel davon)

        for (int i = 0; i <= 4; i++)
        {
            double y = bottom - (bottom - top) * i / 4;
            context.DrawLine(GridPen, new Point(left, y), new Point(right, y));
            var label = new FormattedText($"{maxMs * i / 4:0}", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Font, 10, LabelBrush);
            context.DrawText(label, new Point(0, y - 7));
        }
        var axis = new FormattedText($"letzte {Window.TotalMinutes:0} Min. – ms", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Font, 10, LabelBrush);
        context.DrawText(axis, new Point(left, bottom + 2));

        if (points.Count == 0) return;

        Point Map(PingSample s) => new(
            left + (right - left) * ((s.At - from).TotalMilliseconds / Window.TotalMilliseconds),
            bottom - (bottom - top) * Math.Min(1, s.Ms / maxMs));

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(Map(points[0]), false);
            foreach (var p in points.Skip(1)) g.LineTo(Map(p));
            g.EndFigure(false);
        }
        context.DrawGeometry(null, LinePen, geometry);

        foreach (var p in points.Where(p => p.Ms >= SpikeThreshold))
            context.DrawEllipse(SpikeBrush, null, Map(p), 3, 3);
    }
}
