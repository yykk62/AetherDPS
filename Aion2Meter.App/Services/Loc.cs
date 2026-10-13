using System.Text.Json;
using Avalonia.Markup.Xaml;

namespace Aion2Meter.App.Services;

/// <summary>
/// Texte aus Lang/&lt;sprache&gt;.json. Neue Sprache = neue JSON-Datei, fehlende
/// Schlüssel fallen auf Deutsch und zuletzt auf den Schlüssel selbst zurück.
/// </summary>
public static class Loc
{
    private static Dictionary<string, string> texts = [];
    private static Dictionary<string, string> fallback = [];

    public static void Load(string language = "de")
    {
        fallback = Read("de");
        texts = language == "de" ? fallback : Read(language);
    }

    private static Dictionary<string, string> Read(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Lang", $"{language}.json");
        if (!File.Exists(path)) return [];
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
    }

    public static string T(string key) =>
        texts.TryGetValue(key, out var s) ? s : fallback.TryGetValue(key, out var f) ? f : key;

    public static string T(string key, params object[] args) => string.Format(T(key), args);
}

/// <summary>XAML: Text="{l:T header.reset}"</summary>
public sealed class TExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(key);
}
