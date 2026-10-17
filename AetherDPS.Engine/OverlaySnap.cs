namespace AetherDPS.Engine;

/// <summary>Rechteck in Bildschirm-Pixeln (ohne Avalonia, damit die Engine-Tests es prüfen können).</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

/// <summary>
/// Einrasten im Platzierungsmodus (docs/DESIGN.md, „Eine Schicht für alle Overlays“): an die Ränder des Bildschirms
/// und an andere Overlays – bündig daneben (mit kleinem Abstand) oder Kante an Kante in einer Linie.
/// </summary>
public static class OverlaySnap
{
    /// <summary>Neue linke obere Ecke für <paramref name="moving"/>; bleibt, wo sie ist, wenn nichts nah genug liegt.</summary>
    public static (int X, int Y) Snap(ScreenRect moving, IEnumerable<ScreenRect> others, IEnumerable<ScreenRect> areas, int threshold, int gap)
    {
        var xs = new List<int>();
        var ys = new List<int>();
        foreach (var a in areas)
        {
            xs.Add(a.X);
            xs.Add(a.Right - moving.Width);
            ys.Add(a.Y);
            ys.Add(a.Bottom - moving.Height);
        }
        foreach (var o in others)
        {
            // nur Nachbarn: ein Overlay am anderen Ende des Bildschirms zieht nicht
            bool besideY = moving.Y <= o.Bottom + threshold && o.Y <= moving.Bottom + threshold;
            bool besideX = moving.X <= o.Right + threshold && o.X <= moving.Right + threshold;
            if (besideY)
            {
                xs.Add(o.Right + gap);
                xs.Add(o.X - gap - moving.Width);
                xs.Add(o.X);
                xs.Add(o.Right - moving.Width);
            }
            if (besideX)
            {
                ys.Add(o.Bottom + gap);
                ys.Add(o.Y - gap - moving.Height);
                ys.Add(o.Y);
                ys.Add(o.Bottom - moving.Height);
            }
        }
        return (Nearest(moving.X, xs, threshold), Nearest(moving.Y, ys, threshold));
    }

    private static int Nearest(int value, List<int> candidates, int threshold)
    {
        int best = value, distance = threshold + 1;
        foreach (var c in candidates)
        {
            int d = Math.Abs(c - value);
            if (d < distance)
            {
                best = c;
                distance = d;
            }
        }
        return best;
    }
}

/// <summary>Lage für „Overlays nach Lage“: außerhalb von Dungeons, im Dungeon, im Bosskampf (auch Feldboss).</summary>
public enum OverlaySituation { Town, Dungeon, Boss }

/// <summary>Welche Overlays die Lage zeigt: in der Stadt die Timer, im Dungeon die Kampfanzeige, im Bosskampf zusätzlich Stagger.</summary>
public static class OverlayRules
{
    public static OverlaySituation Of(bool inDungeon, bool bossFight) =>
        bossFight ? OverlaySituation.Boss : inDungeon ? OverlaySituation.Dungeon : OverlaySituation.Town;

    public static bool ShowCombat(OverlaySituation s) => s != OverlaySituation.Town;

    public static bool ShowTimers(OverlaySituation s) => s == OverlaySituation.Town;

    public static bool ShowStagger(OverlaySituation s) => s == OverlaySituation.Boss;
}
