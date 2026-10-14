using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AetherDPS.App.Services;

public static class BoolBrush
{
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#4CC36A"));
    private static readonly IBrush Amber = new SolidColorBrush(Color.Parse("#E0A84A"));
    private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#E86A6A"));
    private static readonly IBrush Text = new SolidColorBrush(Color.Parse("#C9CED6"));

    public static readonly IValueConverter ConnectedGreenElseAmber =
        new FuncValueConverter<bool, IBrush>(connected => connected ? Green : Amber);

    public static readonly IValueConverter LockedGoldElseSubtle =
        new FuncValueConverter<bool, IBrush>(locked => locked ? new SolidColorBrush(Color.Parse("#99E0A84A")) : new SolidColorBrush(Color.Parse("#33FFFFFF")));

    public static readonly IValueConverter DueGreenElseText =
        new FuncValueConverter<bool, IBrush>(due => due ? Green : Text);

    /// <summary>Stagger-Warnung: unter 5 s rot, sonst hell.</summary>
    public static readonly IValueConverter HurryRedElseText =
        new FuncValueConverter<bool, IBrush>(hurry => hurry ? Red : Brushes.White);

    public static readonly IValueConverter ErrorRedElseText =
        new FuncValueConverter<bool, IBrush>(error => error ? Red : Text);
}

/// <summary>Schritt-Kreis: erledigt = grün, aktiv = gold, später = grau.</summary>
public sealed class StepBrush : IValueConverter
{
    public static readonly StepBrush Instance = new();
    private static readonly IBrush Done = new SolidColorBrush(Color.Parse("#4CC36A"));
    private static readonly IBrush Active = new SolidColorBrush(Color.Parse("#E0A84A"));
    private static readonly IBrush Later = new SolidColorBrush(Color.Parse("#4A5160"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int active = value is int a ? a : 1;
        int step = int.TryParse(parameter?.ToString(), out var s) ? s : 1;
        return step < active ? Done : step == active ? Active : Later;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
