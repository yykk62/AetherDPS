using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AetherDPS.App.Services;

public static class BoolBrush
{
    // Farben aus den Design-Tokens (Tokens.axaml), keine Hex-Werte im Code
    private static IBrush Green => TokenBrush.Get("Signal.Good");
    private static IBrush Amber => TokenBrush.Get("Signal.Warn");
    private static IBrush Red => TokenBrush.Get("Signal.Bad");
    private static IBrush Text => TokenBrush.Get("Text.Secondary");
    private static IBrush Muted => TokenBrush.Get("Text.Muted");

    public static readonly IValueConverter ConnectedGreenElseAmber =
        new FuncValueConverter<bool, IBrush>(connected => connected ? Green : Amber);

    public static readonly IValueConverter LockedGoldElseSubtle =
        new FuncValueConverter<bool, IBrush>(locked => locked ? TokenBrush.Get("Text.Primary") : TokenBrush.Get("Line.Frame"));

    public static readonly IValueConverter DueGreenElseText =
        new FuncValueConverter<bool, IBrush>(due => due ? Green : Text);

    /// <summary>Stagger-Warnung: unter 5 s rot, sonst hell.</summary>
    public static readonly IValueConverter HurryRedElseText =
        new FuncValueConverter<bool, IBrush>(hurry => hurry ? Red : TokenBrush.Get("Text.Primary"));

    public static readonly IValueConverter ErrorRedElseText =
        new FuncValueConverter<bool, IBrush>(error => error ? Red : Text);

    /// <summary>Statuszeile unter einer Suche: Fehler rötlich, sonst gedämpft.</summary>
    public static readonly IValueConverter ErrorRedElseMuted =
        new FuncValueConverter<bool, IBrush>(error => error ? Red : Muted);

    /// <summary>Kachel hinter dem Icon der Kampfkarte: Boss rötlich, normales Ziel ruhig.</summary>
    public static readonly IValueConverter BossRedElseGoldTile =
        new FuncValueConverter<bool, IBrush>(boss => boss ? TokenBrush.Get("Surface.Boss") : TokenBrush.Get("Surface.Raised"));

    /// <summary>Hinweiskarten im Hauptfenster: Warnung bernsteinfarben getönt, sonst normale Karte.</summary>
    public static readonly IValueConverter WarningElseCard =
        new FuncValueConverter<bool, IBrush>(warning => warning ? TokenBrush.Get("Surface.NoticeWarn") : TokenBrush.Get("Surface.Notice"));

    /// <summary>Gewählte Zeile (Pet-Radar): erhaben hinterlegt.</summary>
    public static readonly IValueConverter SelectedRow =
        new FuncValueConverter<bool, IBrush>(selected => selected ? TokenBrush.Get("Surface.Raised") : Brushes.Transparent);

    /// <summary>Ausgewähltes Item im Profil: farbiger Rahmen.</summary>
    public static readonly IValueConverter SelectedBorder =
        new FuncValueConverter<bool, Avalonia.Thickness>(selected => new Avalonia.Thickness(selected ? 1 : 0));

    /// <summary>Großer Status-Kreis auf der Übersicht: Meter läuft grün getönt, sonst goldbraun.</summary>
    public static readonly IValueConverter ActiveGreenElseGoldTile =
        new FuncValueConverter<bool, IBrush>(active => active ? TokenBrush.Get("Surface.Raised") : TokenBrush.Get("Surface.Raised"));

    public static readonly IValueConverter WarningBorderElseCard =
        new FuncValueConverter<bool, IBrush>(warning => warning ? TokenBrush.Get("Signal.Warn") : TokenBrush.Get("Line.Frame"));
}

/// <summary>Schritt-Kreis: erledigt = grün, aktiv = hell, später = grau.</summary>
public sealed class StepBrush : IValueConverter
{
    public static readonly StepBrush Instance = new();
    private static IBrush Done => TokenBrush.Get("Signal.Good");
    private static IBrush Active => TokenBrush.Get("Text.Primary");
    private static IBrush Later => TokenBrush.Get("Line.Frame");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int active = value is int a ? a : 1;
        int step = int.TryParse(parameter?.ToString(), out var s) ? s : 1;
        return step < active ? Done : step == active ? Active : Later;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Navigation im Hauptfenster: IsVisible / Classes.active="{Binding SelectedTab, Converter={x:Static l:TabIs.Instance}, ConverterParameter=3}".</summary>
public sealed class TabIs : IValueConverter
{
    public static readonly TabIs Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int tab && int.TryParse(parameter?.ToString(), out var wanted) && tab == wanted;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
