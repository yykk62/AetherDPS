using AionDpsMeter.Core.GameData.Services;

namespace AetherDPS.Engine;

/// <summary>
/// Offizielle Bild-URL (assets.playnccdn.com, Schema aus RATmeter <c>SkillIconResolver</c>) zu einem Skill- oder
/// Buff-Namen. Gespeicherte Läufe kennen nur Namen, deshalb über den Namen in der aktuellen Sprache.
/// Buffs haben keine eigene Bildquelle: Heißt ein Buff wie ein Skill (die meisten Gruppen-Buffs), gilt dessen Bild.
/// </summary>
public static class SkillIcons
{
    private static Dictionary<string, string>? byName;
    private static string? builtFor;

    public static string? ForName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var map = Map();
        if (map.TryGetValue(name.Trim(), out var url)) return url;
        // Beschwörungen tragen ein Präfix („Geist: …“) – dann den Rest nachschlagen
        int colon = name.IndexOf(": ", StringComparison.Ordinal);
        return colon > 0 && map.TryGetValue(name[(colon + 2)..].Trim(), out url) ? url : null;
    }

    private static Dictionary<string, string> Map()
    {
        lock (typeof(SkillIcons))
        {
            if (byName is not null && builtFor == GameText.Language) return byName;
            var map = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var skill in GameDataProvider.Instance.Skills.Skills.Values.OrderBy(s => s.Id))
                if (!string.IsNullOrWhiteSpace(skill.Name) && skill.Icon is { Length: > 0 } icon && !map.ContainsKey(skill.Name))
                    map[skill.Name] = icon;
            builtFor = GameText.Language;
            return byName = map;
        }
    }
}
