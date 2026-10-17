using AetherDPS.Engine;

namespace AetherDPS.Tests;

/// <summary>Nur echte Dungeons fließen in Vergleiche und Bestzeiten ein.</summary>
public sealed class RunCategoryTests
{
    private static ArchivedRun Run(string name, int dungeon = 0, int map = 0, bool instance = false) =>
        new("x", DateTime.Now, 60, 40, name, 1000, 10, [], [], dungeon, map, instance);

    [Fact]
    public void Ordnet_Laeufe_ein()
    {
        GameText.Apply("de");
        Assert.Equal(RunKind.Expedition, RunCategory.Of(Run("Krao", dungeon: 600002, map: 600002, instance: true)));   // Erkundung
        Assert.Equal(RunKind.Conquest, RunCategory.Of(Run("Krao", dungeon: 600004, map: 600004, instance: true)));     // Eroberung (schwer)
        Assert.Equal(RunKind.Transcendence, RunCategory.Of(Run("Arkanis", dungeon: 600062, map: 600062, instance: true)));
        Assert.Equal(RunKind.Abyss, RunCategory.Of(Run("Nahma", map: 20)));
        Assert.Equal(RunKind.World, RunCategory.Of(Run("Henker Nemok", map: 1010)));
        Assert.Equal(RunKind.Instance, RunCategory.Of(Run("Quest", map: 650001, instance: true)));
        // alte Läufe ohne Ids: über den Dungeon-Namen, sonst unbekannt
        Assert.Equal(RunKind.Expedition, RunCategory.Of(Run(GameText.DungeonName(600002)!)));
        Assert.Equal(RunKind.Unknown, RunCategory.Of(Run("Henker Nemok")));
        Assert.False(RunCategory.IsComparable(Run("Henker Nemok", map: 1010)));
        Assert.True(RunCategory.IsComparable(Run("Nahma", map: 20)));
    }
}
