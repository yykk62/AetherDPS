using System.Text.Json;
using AionDpsMeter.Core.GameData.Repositories;
using AionDpsMeter.Core.GameData.Services;
using AionDpsMeter.Core.Models;

namespace AetherDPS.Engine;

/// <summary>
/// Spieltexte in der gewählten Sprache: Skills, Mobs, Klassen, Dungeons und Server.
/// Quelle: A2Tools (GPL-3.0), siehe tools/update-game-data.ps1. Fehlt ein Eintrag in der
/// Sprache, gilt Englisch und zuletzt der Name aus den Upstream-Daten.
/// </summary>
public static class GameText
{
    public static readonly string[] Languages = ["de", "en", "fr", "es", "pt", "ru", "ja", "ko", "zh-Hans", "zh-Hant"];

    // Klassen-Ids wie im Spiel (job_class.rs bei A2Tools) → Schlüssel in ui.json
    private static readonly Dictionary<int, string> ClassKeys = new()
    {
        [11] = "GLADIATOR", [12] = "TEMPLAR", [13] = "ASSASSIN", [14] = "RANGER", [15] = "SORCERER",
        [16] = "ELEMENTALIST", [17] = "CLERIC", [18] = "CHANTER", [19] = "FIGHTER",
    };

    private static MobRepository? mobs;
    private static Dictionary<int, string> classNames = [];
    private static Dictionary<int, (string Name, string? Difficulty)> dungeons = [];
    private static Dictionary<string, string> difficulties = [];
    private static Dictionary<int, string> servers = [];
    private static HashSet<int> openWorldMaps = [];

    public static string Language { get; private set; } = "de";
    public static int PatchedSkillCount { get; private set; }

    /// <summary>Einmal beim Start aufrufen, bevor die Paketverarbeitung läuft.</summary>
    public static void Apply(string language = "de", string? dataDir = null)
    {
        dataDir ??= Path.Combine(AppContext.BaseDirectory, "GameData");
        Language = Languages.Contains(language) ? language : "en";
        string? File(string name) => Find(dataDir, Language, name);

        if (File("skills.json") is { } skillsPath)
        {
            var names = ReadMap(skillsPath, e => e.ValueKind == JsonValueKind.String ? e.GetString() : null);
            var skills = GameDataProvider.Instance.Skills.Skills;
            int patched = 0;
            foreach (var (id, skill) in skills.ToList())
            {
                if (!names.TryGetValue(id, out var name) || string.IsNullOrWhiteSpace(name)) continue;
                skills[id] = new Skill
                {
                    Id = skill.Id,
                    ClassId = skill.ClassId,
                    Name = name,
                    Icon = skill.Icon,
                    SpecializationFlags = skill.SpecializationFlags,
                };
                patched++;
            }
            PatchedSkillCount = patched;
        }

        if (File("npcs.json") is { } npcsPath)
        {
            var repo = new MobRepository();
            repo.Load(npcsPath);
            mobs = repo;
        }

        classNames = [];
        difficulties = [];
        if (File("ui.json") is { } uiPath)
        {
            using var ui = JsonDocument.Parse(System.IO.File.ReadAllText(uiPath));
            if (ui.RootElement.TryGetProperty("classes", out var classes))
                foreach (var (id, key) in ClassKeys)
                    if (classes.TryGetProperty(key, out var n) && n.GetString() is { Length: > 0 } name) classNames[id] = name;
            if (ui.RootElement.TryGetProperty("dungeon", out var d) && d.TryGetProperty("difficulty", out var diff))
                foreach (var p in diff.EnumerateObject())
                    if (p.Value.GetString() is { } text) difficulties[p.Name] = text;
        }

        dungeons = [];
        if (File("dungeons.json") is { } dungeonsPath)
        {
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(dungeonsPath));
            foreach (var p in doc.RootElement.EnumerateObject())
                if (int.TryParse(p.Name, out var id) && p.Value.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } name)
                    dungeons[id] = (name, p.Value.TryGetProperty("difficulty", out var d) ? d.GetString() : null);
        }

        servers = [];
        var serversPath = Path.Combine(dataDir, "servers.json");
        if (System.IO.File.Exists(serversPath))
        {
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(serversPath));
            foreach (var s in doc.RootElement.GetProperty("servers").EnumerateObject())
            {
                if (!int.TryParse(s.Name, out var id)) continue;
                var name = s.Value.TryGetProperty(Language, out var n) ? n.GetString()
                    : s.Value.TryGetProperty("en", out var en) ? en.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name)) servers[id] = name;
            }
        }

        var mapsPath = Path.Combine(dataDir, "open_world_maps.json");
        if (System.IO.File.Exists(mapsPath))
        {
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(mapsPath));
            openWorldMaps = doc.RootElement.GetProperty("maps").EnumerateArray().Select(m => m.GetInt32()).ToHashSet();
        }
    }

    /// <summary>Datei in der Sprache, sonst auf Englisch.</summary>
    private static string? Find(string dataDir, string language, string name)
    {
        foreach (var lang in new[] { language, "en" })
        {
            var path = Path.Combine(dataDir, lang, name);
            if (System.IO.File.Exists(path)) return path;
        }
        return null;
    }

    private static Dictionary<int, T> ReadMap<T>(string path, Func<JsonElement, T?> read)
    {
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
        var result = new Dictionary<int, T>();
        foreach (var p in doc.RootElement.EnumerateObject())
            if (int.TryParse(p.Name, out var id) && read(p.Value) is { } value) result[id] = value;
        return result;
    }

    public static string MobName(Mob mob) =>
        mobs is not null && mobs.IsKnownMob(mob.MobCode) ? mobs.GetName(mob.MobCode) : mob.Name;

    /// <summary>NPC-Name nach Code in der gewählten Sprache, null wenn unbekannt.</summary>
    public static string? NpcName(int code) => mobs is not null && mobs.IsKnownMob(code) ? mobs.GetName(code) : null;

    public static string ClassName(int classId, string fallback) =>
        classNames.TryGetValue(classId, out var name) ? name : fallback;

    /// <summary>„Krao Cave (Schwer)“, ohne Schwierigkeit nur der Name; unbekannt → null.</summary>
    public static string? DungeonName(int mapId)
    {
        if (!dungeons.TryGetValue(mapId, out var d)) return null;
        return d.Difficulty is { } key && difficulties.TryGetValue(key, out var diff) ? $"{d.Name} ({diff})" : d.Name;
    }

    /// <summary>Instanz-Karten (Dungeons) haben Ids ab 600000, die offene Welt steht in open_world_maps.json.</summary>
    public static bool IsInstanceMap(int mapId) => mapId is >= 600_000 and < 700_000 && !openWorldMaps.Contains(mapId);

    public static string? ServerName(int id) => servers.GetValueOrDefault(id);

    /// <summary>Alle bekannten Server (ID → Name in der gewählten Sprache).</summary>
    public static IReadOnlyDictionary<int, string> Servers => servers;
}
