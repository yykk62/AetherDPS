using System.Text.Json;

namespace AetherDPS.Engine;

/// <summary>Art eines Laufs. Nur die ersten fünf fließen in Vergleiche, Bestzeiten und die Messlatte ein.</summary>
public enum RunKind { Expedition, Conquest, Transcendence, Daily, Abyss, Instance, World, Unknown }

/// <summary>
/// Ordnet einen Lauf einer Kategorie zu (GameData/dungeon_categories.json): Expedition (Erkundung), Eroberung
/// (derselbe Expeditions-Dungeon im schweren Modus, erkennbar an der Schwierigkeit normal/hard), Transzendenz,
/// täglicher Dungeon, Abyss. Unbekannte Instanzen (Quest-, versiegelte Dungeons), die offene Welt und alte Läufe ohne
/// erkennbaren Dungeon zählen nicht als vergleichbar – sonst verfälscht ein Quest-Dungeon von einer Minute die Bestzeit.
/// </summary>
public static class RunCategory
{
    private static Dictionary<int, RunKind>? byDungeon;
    private static HashSet<int> abyssMaps = [];

    public static RunKind Of(ArchivedRun run)
    {
        Load();
        if (run.DungeonId > 0) return Dungeon(run.DungeonId);
        if (run.MapId > 0)
            return abyssMaps.Contains(run.MapId) ? RunKind.Abyss : run.InInstance ? RunKind.Instance : RunKind.World;
        // ältere Läufe kennen nur den Namen: „Krao Cave (Schwer)“ → Dungeon
        return DungeonIdByName(run.Name) is int id ? Dungeon(id) : RunKind.Unknown;
    }

    /// <summary>Expeditions-Dungeon mit Schwierigkeit normal/hard ist Eroberung, mit exploration (oder ohne) Erkundung.</summary>
    private static RunKind Dungeon(int id)
    {
        var kind = byDungeon!.GetValueOrDefault(id, RunKind.Instance);
        return kind == RunKind.Expedition && GameText.DungeonDifficulty(id) is "normal" or "hard" ? RunKind.Conquest : kind;
    }

    public static bool IsComparable(RunKind kind) =>
        kind is RunKind.Expedition or RunKind.Conquest or RunKind.Transcendence or RunKind.Daily or RunKind.Abyss;

    public static bool IsComparable(ArchivedRun run) => IsComparable(Of(run));

    /// <summary>Text-Schlüssel der Kategorie (Lang: runkind.*).</summary>
    public static string Key(RunKind kind) => "runkind." + kind.ToString().ToLowerInvariant();

    private static int? DungeonIdByName(string name)
    {
        foreach (var id in GameText.DungeonIds)
            if (string.Equals(GameText.DungeonName(id), name, StringComparison.CurrentCultureIgnoreCase)) return id;
        return null;
    }

    private static void Load()
    {
        if (byDungeon is not null) return;
        var map = new Dictionary<int, RunKind>();
        var file = Path.Combine(AppContext.BaseDirectory, "GameData", "dungeon_categories.json");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            void Add(string prop, RunKind kind)
            {
                if (doc.RootElement.TryGetProperty(prop, out var list))
                    foreach (var id in list.EnumerateArray()) map[id.GetInt32()] = kind;
            }
            Add("expedition", RunKind.Expedition);
            Add("transcendence", RunKind.Transcendence);
            Add("daily", RunKind.Daily);
            if (doc.RootElement.TryGetProperty("abyssMaps", out var abyss))
                abyssMaps = abyss.EnumerateArray().Select(e => e.GetInt32()).ToHashSet();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        byDungeon = map;
    }
}
