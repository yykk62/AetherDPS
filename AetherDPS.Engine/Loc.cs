using System.Globalization;
using System.Text.Json;

namespace AetherDPS.Engine;

/// <summary>
/// Texte aus Lang/&lt;sprache&gt;.json. Neue Sprache = neue JSON-Datei. Fehlende Schlüssel
/// fallen auf Englisch, dann Deutsch und zuletzt auf den Schlüssel selbst zurück.
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, string> Cultures = new()
    {
        ["de"] = "de-DE", ["en"] = "en-US", ["fr"] = "fr-FR", ["es"] = "es-ES", ["pt"] = "pt-BR",
        ["ru"] = "ru-RU", ["ja"] = "ja-JP", ["ko"] = "ko-KR", ["zh-Hans"] = "zh-CN", ["zh-Hant"] = "zh-TW",
    };

    private static List<Dictionary<string, string>>? chain;

    public static string Language { get; private set; } = "de";

    /// <summary>Zahlen und Datum im Format der Sprache.</summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Sprachen mit eigener Textdatei, in der Reihenfolge der Auswahlliste.</summary>
    public static IReadOnlyList<string> Available => GameText.Languages;

    /// <summary>„auto“ = Sprache von Windows, sonst Englisch.</summary>
    public static string Resolve(string? setting)
    {
        if (!string.IsNullOrWhiteSpace(setting) && setting != "auto" && Cultures.ContainsKey(setting)) return setting;
        var ui = CultureInfo.CurrentUICulture;
        if (ui.TwoLetterISOLanguageName == "zh")
            return ui.Name.Contains("TW") || ui.Name.Contains("HK") || ui.Name.Contains("Hant") ? "zh-Hant" : "zh-Hans";
        return Cultures.ContainsKey(ui.TwoLetterISOLanguageName) ? ui.TwoLetterISOLanguageName : "en";
    }

    public static void Load(string language = "de")
    {
        Language = Cultures.ContainsKey(language) ? language : "en";
        Culture = CultureInfo.GetCultureInfo(Cultures[Language]);
        chain = new[] { Language, "en", "de" }.Distinct().Select(Read).Where(d => d.Count > 0).ToList();
    }

    private static Dictionary<string, string> Read(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Lang", $"{language}.json");
        if (!File.Exists(path)) return [];
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
    }

    public static string T(string key)
    {
        if (chain is null) Load();   // Tests und Werkzeuge ohne ausdrückliches Laden: Deutsch
        foreach (var texts in chain!)
            if (texts.TryGetValue(key, out var s)) return s;
        return key;
    }

    public static string T(string key, params object[] args) => string.Format(Culture, T(key), args);
}
