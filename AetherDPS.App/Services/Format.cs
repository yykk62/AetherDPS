using System.Globalization;
using AetherDPS.Engine;

namespace AetherDPS.App.Services;

public static class Format
{
    private static CultureInfo De => Loc.Culture;

    /// <summary>Kompakt mit deutschem Komma: 950 · 12,3K · 1,23M · 2,1B</summary>
    public static string Compact(double value) => double.IsFinite(value) ? CompactFinite(value) : "0";

    private static string CompactFinite(double value) => Math.Abs(value) switch
    {
        >= 1e9 => (value / 1e9).ToString("0.##", De) + "B",
        >= 1e6 => (value / 1e6).ToString("0.##", De) + "M",
        >= 1e4 => (value / 1e3).ToString("0.#", De) + "K",
        _ => value.ToString("N0", De),
    };

    /// <summary>Kompakt mit fester Nachkommastelle, damit Spalten gleich lang bleiben: 83,0K · 110,8K · 1,23M</summary>
    public static string CompactFixed(double value) => !double.IsFinite(value) ? "0" : Math.Abs(value) switch
    {
        >= 1e9 => (value / 1e9).ToString("0.00", De) + "B",
        >= 1e6 => (value / 1e6).ToString("0.00", De) + "M",
        >= 1e4 => (value / 1e3).ToString("0.0", De) + "K",
        _ => value.ToString("N0", De),
    };

    /// <summary>Ganze Zahl mit Tausenderpunkten: 10.681.200</summary>
    public static string Number(long value) => value.ToString("N0", De);

    public static string Percent(double value) => value.ToString("0.0", De) + " %";
}
