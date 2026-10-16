using System.Collections.Concurrent;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AetherDPS.App.Services;

/// <summary>
/// Eigenes Icon-Set (assets/brand, Relief-Stil: Gold mit violettem Schein), eingebettet unter Assets/icons/&lt;px&gt;/&lt;name&gt;.png.
/// Für eine logische Größe wird die Datei mit mindestens der doppelten Pixelzahl geladen, damit es bei 150 %/200 % scharf bleibt.
/// </summary>
public static class Icons
{
    private static readonly int[] Sizes = [16, 24, 32, 48, 64];
    private static readonly ConcurrentDictionary<string, Bitmap?> cache = new();

    /// <summary>Bitmap für ein Icon, das mit <paramref name="logical"/> Pixeln Kantenlänge gezeichnet wird.</summary>
    public static Bitmap? Get(string name, double logical = 24)
    {
        int want = (int)Math.Ceiling(logical * 2);
        int px = Sizes.FirstOrDefault(s => s >= want, Sizes[^1]);
        return cache.GetOrAdd($"{px}/{name}", key =>
        {
            try
            {
                return new Bitmap(AssetLoader.Open(new Uri($"avares://AetherDPS/Assets/icons/{key}.png")));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Icon {key}: {e}");
                return null; // unbekannter Name: lieber leer als Absturz
            }
        });
    }

    /// <summary>Binding auf einen Icon-Namen (z. B. Timer-Art): Source="{Binding Icon, Converter={x:Static l:Icons.ByName}}".</summary>
    public static readonly IValueConverter ByName = new FuncValueConverter<string?, Bitmap?>(n => n is null ? null : Get(n, 16));

    /// <summary>★ / ☆ als Icon: Favorit an/aus.</summary>
    public static readonly IValueConverter StarFilledElseOutline =
        new FuncValueConverter<bool, Bitmap?>(on => Get(on ? "star" : "star-outline", 14));

    /// <summary>Statuszeile: Warnung bei Fehler, sonst Info.</summary>
    public static readonly IValueConverter WarningElseInfo =
        new FuncValueConverter<bool, Bitmap?>(bad => Get(bad ? "warning" : "info", 12));
}

/// <summary>XAML: Source="{l:Icon close, Size=12}" – Bitmap aus dem eigenen Icon-Set.</summary>
public sealed class IconExtension(string name) : MarkupExtension
{
    /// <summary>Logische Kantenlänge, mit der das Bild gezeichnet wird (bestimmt die geladene Auflösung).</summary>
    public double Size { get; set; } = 24;

    public override object? ProvideValue(IServiceProvider serviceProvider) => Icons.Get(name, Size);
}
