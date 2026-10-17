using AetherDPS.Engine.Community;

namespace AetherDPS.Tests;

/// <summary>Verlauf des eigenen Charakters: Punkte, Zusammenfassen gleicher Werte, Speichern.</summary>
public sealed class ProfileHistoryTests : IDisposable
{
    private readonly string file = Path.Combine(Path.GetTempPath(), "aetherdps-verlauf-" + Guid.NewGuid().ToString("N") + ".json");

    private static ArmoryProfile Profile(int cp, int il) =>
        new("Testheld", "Hogalum", 1317, "Waldläufer", 45, il, cp, "Elyos", [new ArmoryItem("MainHand", "Bogen", "Epic", 5, null)], [], DateTime.Now);

    [Fact]
    public void Haelt_Punkte_fest_und_fasst_gleiche_zusammen()
    {
        var history = new ProfileHistory(file);
        var t = new DateTime(2026, 10, 1, 12, 0, 0);
        history.Add(Profile(70_000, 1_300), t);
        history.Add(Profile(70_000, 1_300), t.AddMinutes(20));   // gleiche Werte, gleiche Stunde → kein neuer Punkt
        history.Add(Profile(70_500, 1_305), t.AddHours(4));
        history.Add(Profile(70_500, 1_305), t.AddHours(8));      // gleiche Werte, aber später → neuer Punkt
        var points = history.Points("testheld", 1317);
        Assert.Equal(3, points.Count);
        Assert.Equal(70_500, points[^1].CombatPower);
        Assert.Equal("MainHand", Assert.Single(history.Latest("Testheld", 1317)!.Gear).Slot);

        var reloaded = new ProfileHistory(file);   // aus der Datei
        Assert.Equal(3, reloaded.Points("Testheld", 1317).Count);
        Assert.Empty(reloaded.Points("Testheld", 2317));
    }

    public void Dispose()
    {
        try { File.Delete(file); } catch (IOException) { }
    }
}
