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

    /// <summary>Statuszeile unter einer Suche: Fehler rötlich, sonst gedämpft.</summary>
    public static readonly IValueConverter ErrorRedElseMuted =
        new FuncValueConverter<bool, IBrush>(error => error ? Red : new SolidColorBrush(Color.Parse("#8E96A3")));

    /// <summary>Kachel hinter dem Icon der Kampfkarte: Boss rötlich, normales Ziel goldbraun.</summary>
    public static readonly IValueConverter BossRedElseGoldTile =
        new FuncValueConverter<bool, IBrush>(boss => boss ? new SolidColorBrush(Color.Parse("#3A1F24")) : new SolidColorBrush(Color.Parse("#2A2416")));

    /// <summary>Hinweiskarten im Hauptfenster: Warnung bernsteinfarben getönt, sonst normale Karte.</summary>
    public static readonly IValueConverter WarningElseCard =
        new FuncValueConverter<bool, IBrush>(warning => new SolidColorBrush(Color.Parse(warning ? "#2A2418" : "#191E28")));

    /// <summary>Gewählte Zeile (Pet-Radar): leicht golden hinterlegt.</summary>
    public static readonly IValueConverter SelectedRow =
        new FuncValueConverter<bool, IBrush>(selected => new SolidColorBrush(Color.Parse(selected ? "#40E0A84A" : "#00000000")));

    /// <summary>Ausgewähltes Item im Profil: farbiger Rahmen.</summary>
    public static readonly IValueConverter SelectedBorder =
        new FuncValueConverter<bool, Avalonia.Thickness>(selected => new Avalonia.Thickness(selected ? 1 : 0));

    /// <summary>Großer Status-Kreis auf der Übersicht: Meter läuft grün getönt, sonst goldbraun.</summary>
    public static readonly IValueConverter ActiveGreenElseGoldTile =
        new FuncValueConverter<bool, IBrush>(active => new SolidColorBrush(Color.Parse(active ? "#1F3A2A" : "#2A2416")));

    public static readonly IValueConverter WarningBorderElseCard =
        new FuncValueConverter<bool, IBrush>(warning => new SolidColorBrush(Color.Parse(warning ? "#7A5A22" : "#2E3746")));
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

/// <summary>Navigation im Hauptfenster: IsVisible / Classes.active="{Binding SelectedTab, Converter={x:Static l:TabIs.Instance}, ConverterParameter=3}".</summary>
public sealed class TabIs : IValueConverter
{
    public static readonly TabIs Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int tab && int.TryParse(parameter?.ToString(), out var wanted) && tab == wanted;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
