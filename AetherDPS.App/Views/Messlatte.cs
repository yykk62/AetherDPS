using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AetherDPS.App.Views;

/// <summary>
/// Gravierte Skala, die sich selbst erklärt: frühere Werte als Striche (der bisher beste heller und länger), der Schnitt als beschriftetes
/// Dreieck, der aktuelle Wert als goldene Nadel mit Beschriftung („heute 98,2K“). An den Enden steht die
/// Richtung („weniger … mehr“); rechts ist immer besser, bei Zeiten mit <see cref="LowerIsBetter"/>
/// gespiegelt. Farben kommen aus den Tokens (docs/DESIGN.md).
/// </summary>
public sealed class Messlatte : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> HistoryProperty =
        AvaloniaProperty.Register<Messlatte, IReadOnlyList<double>?>(nameof(History));
    public static readonly StyledProperty<double> CurrentProperty =
        AvaloniaProperty.Register<Messlatte, double>(nameof(Current), double.NaN);
    public static readonly StyledProperty<double> AverageProperty =
        AvaloniaProperty.Register<Messlatte, double>(nameof(Average), double.NaN);
    public static readonly StyledProperty<double> BestProperty =
        AvaloniaProperty.Register<Messlatte, double>(nameof(Best), double.NaN);
    public static readonly StyledProperty<IBrush?> BestBrushProperty =
        AvaloniaProperty.Register<Messlatte, IBrush?>(nameof(BestBrush));
    public static readonly StyledProperty<bool> LowerIsBetterProperty =
        AvaloniaProperty.Register<Messlatte, bool>(nameof(LowerIsBetter));
    public static readonly StyledProperty<string?> NeedleTextProperty =
        AvaloniaProperty.Register<Messlatte, string?>(nameof(NeedleText));
    public static readonly StyledProperty<string?> AverageTextProperty =
        AvaloniaProperty.Register<Messlatte, string?>(nameof(AverageText));
    public static readonly StyledProperty<string?> WorseTextProperty =
        AvaloniaProperty.Register<Messlatte, string?>(nameof(WorseText));
    public static readonly StyledProperty<string?> BetterTextProperty =
        AvaloniaProperty.Register<Messlatte, string?>(nameof(BetterText));
    public static readonly StyledProperty<IBrush?> SlotBrushProperty =
        AvaloniaProperty.Register<Messlatte, IBrush?>(nameof(SlotBrush));
    public static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<Messlatte, IBrush?>(nameof(TickBrush));
    public static readonly StyledProperty<IBrush?> NeedleBrushProperty =
        AvaloniaProperty.Register<Messlatte, IBrush?>(nameof(NeedleBrush));
    public static readonly StyledProperty<IBrush?> AverageBrushProperty =
        AvaloniaProperty.Register<Messlatte, IBrush?>(nameof(AverageBrush));
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<Messlatte, IBrush?>(nameof(LabelBrush));
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<Messlatte>();

    static Messlatte()
    {
        AffectsRender<Messlatte>(HistoryProperty, CurrentProperty, AverageProperty, BestProperty, BestBrushProperty, LowerIsBetterProperty,
            NeedleTextProperty, AverageTextProperty, WorseTextProperty, BetterTextProperty,
            SlotBrushProperty, TickBrushProperty, NeedleBrushProperty, AverageBrushProperty, LabelBrushProperty);
    }

    public IReadOnlyList<double>? History { get => GetValue(HistoryProperty); set => SetValue(HistoryProperty, value); }
    public double Current { get => GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }
    public double Average { get => GetValue(AverageProperty); set => SetValue(AverageProperty, value); }
    /// <summary>Bisher bester früherer Wert: heller, längerer Strich.</summary>
    public double Best { get => GetValue(BestProperty); set => SetValue(BestProperty, value); }
    public IBrush? BestBrush { get => GetValue(BestBrushProperty); set => SetValue(BestBrushProperty, value); }
    public bool LowerIsBetter { get => GetValue(LowerIsBetterProperty); set => SetValue(LowerIsBetterProperty, value); }
    public string? NeedleText { get => GetValue(NeedleTextProperty); set => SetValue(NeedleTextProperty, value); }
    public string? AverageText { get => GetValue(AverageTextProperty); set => SetValue(AverageTextProperty, value); }
    public string? WorseText { get => GetValue(WorseTextProperty); set => SetValue(WorseTextProperty, value); }
    public string? BetterText { get => GetValue(BetterTextProperty); set => SetValue(BetterTextProperty, value); }
    public IBrush? SlotBrush { get => GetValue(SlotBrushProperty); set => SetValue(SlotBrushProperty, value); }
    public IBrush? TickBrush { get => GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }
    public IBrush? NeedleBrush { get => GetValue(NeedleBrushProperty); set => SetValue(NeedleBrushProperty, value); }
    public IBrush? AverageBrush { get => GetValue(AverageBrushProperty); set => SetValue(AverageBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    // Aufbau von oben: Nadel-Beschriftung, Raute, Rille mit Strichen, Schnitt-Dreieck + Text, Richtung
    private const double LabelTop = 0, DiamondTop = 22, SlotY = 38, SlotHeight = 6, TriangleTop = 52;
    private const double Inset = 64;   // Platz an den Enden für die Richtungswörter

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width, 70);

    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        var history = History ?? [];
        if (width <= 2 * Inset || double.IsNaN(Current)) return;

        var all = history.Append(Current).Where(double.IsFinite).ToList();
        double lo = all.Min(), hi = all.Max();
        double pad = Math.Max((hi - lo) * 0.08, Math.Abs(hi) * 0.02 + 1e-9);
        lo -= pad;
        hi += pad;
        double span = width - 2 * Inset;
        double X(double v)
        {
            double t = (v - lo) / (hi - lo);
            return Math.Round(Inset + (LowerIsBetter ? 1 - t : t) * span) + 0.5;
        }
        var culture = CultureInfo.CurrentUICulture;
        var plain = new Typeface(FontFamily);
        var strong = new Typeface(FontFamily, weight: FontWeight.SemiBold);
        FormattedText Text(string s, Typeface face, double size, IBrush? brush) =>
            new(s, culture, FlowDirection.LeftToRight, face, size, brush);

        // Rille über die ganze Breite, die Enden bleiben frei für die Richtung
        context.DrawRectangle(SlotBrush, null, new RoundedRect(new Rect(Inset - 8, SlotY, span + 16, SlotHeight), SlotHeight / 2));

        // Richtung: links schlechter, rechts besser (mit Pfeil)
        if (WorseText is { Length: > 0 } worse)
            context.DrawText(Text(worse, plain, 12, LabelBrush), new Point(0, SlotY - 5));
        if (BetterText is { Length: > 0 } better)
        {
            var t = Text(better, plain, 12, LabelBrush);
            context.DrawText(t, new Point(width - t.Width, SlotY - 5));
        }

        // frühere Werte
        var tick = new Pen(TickBrush, 1.5);
        foreach (var v in history.Where(double.IsFinite))
            context.DrawLine(tick, new Point(X(v), SlotY - 6), new Point(X(v), SlotY + SlotHeight + 6));
        if (double.IsFinite(Best))
            context.DrawLine(new Pen(BestBrush ?? TickBrush, 2), new Point(X(Best), SlotY - 10), new Point(X(Best), SlotY + SlotHeight + 10));

        // Schnitt: Dreieck unter der Rille, Text daneben (zur Mitte hin)
        if (double.IsFinite(Average))
        {
            double ax = X(Average);
            var tri = new StreamGeometry();
            using (var g = tri.Open())
            {
                g.BeginFigure(new Point(ax, TriangleTop), true);
                g.LineTo(new Point(ax - 5, TriangleTop + 7));
                g.LineTo(new Point(ax + 5, TriangleTop + 7));
                g.EndFigure(true);
            }
            context.DrawGeometry(AverageBrush, null, tri);
            if (AverageText is { Length: > 0 } avg)
            {
                var t = Text(avg, plain, 12, AverageBrush);
                double x = ax < width / 2 ? ax + 9 : ax - 9 - t.Width;
                context.DrawText(t, new Point(x, TriangleTop - 2));
            }
        }

        // Nadel: Schein, Strich, Raute, Beschriftung darüber (im Rahmen gehalten)
        double nx = X(Current);
        if (NeedleBrush is ISolidColorBrush solid)
            context.DrawRectangle(new SolidColorBrush(solid.Color, 0.22), null,
                new RoundedRect(new Rect(nx - 3, DiamondTop, 6, SlotY + SlotHeight + 8 - DiamondTop), 3));
        context.DrawLine(new Pen(NeedleBrush, 2), new Point(nx, DiamondTop + 6), new Point(nx, SlotY + SlotHeight + 8));
        var diamond = new StreamGeometry();
        using (var g = diamond.Open())
        {
            g.BeginFigure(new Point(nx, DiamondTop), true);
            g.LineTo(new Point(nx + 5, DiamondTop + 5));
            g.LineTo(new Point(nx, DiamondTop + 10));
            g.LineTo(new Point(nx - 5, DiamondTop + 5));
            g.EndFigure(true);
        }
        context.DrawGeometry(NeedleBrush, null, diamond);
        if (NeedleText is { Length: > 0 } needle)
        {
            var t = Text(needle, strong, 14, NeedleBrush);
            double x = Math.Clamp(nx - t.Width / 2, 0, width - t.Width);
            context.DrawText(t, new Point(x, LabelTop));
        }
    }
}
