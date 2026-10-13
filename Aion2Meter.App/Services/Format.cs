using System.Globalization;

namespace Aion2Meter.App.Services;

public static class Format
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Kompakt mit deutschem Komma: 950 · 12,3K · 1,23M · 2,1B</summary>
    public static string Compact(double value) => Math.Abs(value) switch
    {
        >= 1e9 => (value / 1e9).ToString("0.##", De) + "B",
        >= 1e6 => (value / 1e6).ToString("0.##", De) + "M",
        >= 1e4 => (value / 1e3).ToString("0.#", De) + "K",
        _ => value.ToString("N0", De),
    };

    public static string Percent(double value) => value.ToString("0.0", De) + " %";
}
