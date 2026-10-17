using AetherDPS.Engine;

namespace AetherDPS.Tests;

/// <summary>Platzierungsmodus: Einrasten an Bildschirmrand und Nachbarn, Overlays nach Lage.</summary>
public sealed class OverlaySnapTests
{
    private static readonly ScreenRect Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void Rastet_am_Bildschirmrand_ein()
    {
        var moving = new ScreenRect(9, 1080 - 200 - 7, 380, 200);
        Assert.Equal((0, 880), OverlaySnap.Snap(moving, [], [Screen], 12, 6));
        // rechts unten
        moving = new ScreenRect(1920 - 380 - 10, 1080 - 200 + 5, 380, 200);
        Assert.Equal((1540, 880), OverlaySnap.Snap(moving, [], [Screen], 12, 6));
    }

    [Fact]
    public void Bleibt_stehen_wenn_nichts_nah_ist()
    {
        var moving = new ScreenRect(500, 400, 300, 100);
        Assert.Equal((500, 400), OverlaySnap.Snap(moving, [new ScreenRect(1200, 800, 100, 40)], [Screen], 12, 6));
    }

    [Fact]
    public void Rastet_buendig_neben_einem_anderen_Overlay_ein()
    {
        var timer = new ScreenRect(1000, 300, 150, 46);
        // knapp rechts daneben, leicht versetzt: Abstand wird 6 px, oben bündig
        var moving = new ScreenRect(1150 + 10, 304, 150, 46);
        Assert.Equal((1156, 300), OverlaySnap.Snap(moving, [timer], [Screen], 12, 6));
        // knapp darunter: linke Kanten in einer Linie
        moving = new ScreenRect(996, 346 + 9, 150, 46);
        Assert.Equal((1000, 352), OverlaySnap.Snap(moving, [timer], [Screen], 12, 6));
    }

    [Fact]
    public void Ein_ferner_Nachbar_zieht_nicht()
    {
        // gleiche Höhe, aber weit unten auf dem Bildschirm: keine Kante in einer Linie
        var far = new ScreenRect(1000, 900, 150, 46);
        var moving = new ScreenRect(1004, 100, 150, 46);
        Assert.Equal((1004, 100), OverlaySnap.Snap(moving, [far], [Screen], 12, 6));
    }

    [Theory]
    [InlineData(false, false, OverlaySituation.Town, false, true, false)]
    [InlineData(true, false, OverlaySituation.Dungeon, true, false, false)]
    [InlineData(true, true, OverlaySituation.Boss, true, false, true)]
    [InlineData(false, true, OverlaySituation.Boss, true, false, true)]   // Feldboss in der offenen Welt
    public void Overlays_nach_Lage(bool dungeon, bool boss, OverlaySituation expected, bool combat, bool timers, bool stagger)
    {
        var s = OverlayRules.Of(dungeon, boss);
        Assert.Equal(expected, s);
        Assert.Equal(combat, OverlayRules.ShowCombat(s));
        Assert.Equal(timers, OverlayRules.ShowTimers(s));
        Assert.Equal(stagger, OverlayRules.ShowStagger(s));
    }
}
