namespace Aion2Meter.Engine;

/// <summary>Eine Zeile der Schadensliste, unabhängig von Ansicht (Lauf/Ziel).</summary>
public sealed record MeterLine(long PlayerId, string PlayerName, bool IsUser, bool IsIdentified, int ClassId,
    string ClassName, long TotalDamage, double DamagePerSecond, double DamagePercentage);

/// <summary>
/// Beschwörungen (Geister des Beschwörers, Elementare des Magiers …) schickt das Spiel als
/// eigene Einheiten mit wechselnden IDs. Upstream ordnet sie nicht immer dem Besitzer zu –
/// dann tauchen sie als viele namenlose „Spieler“ derselben Klasse auf (Dungeon 13.10.:
/// 5 echte Spieler + 18 namenlose Beschwörer/Magier mit Kleinstschaden).
/// Regel: Ein namenloser Eintrag wird dem einzigen bekannten Spieler derselben Klasse
/// zugeschlagen. Gibt es keinen oder mehrere, bleibt er stehen (oder fliegt im
/// Gruppenmodus raus, siehe <paramref name="dropUnmerged"/>).
/// </summary>
public static class SummonMerger
{
    public sealed record Result(List<MeterLine> Lines, Dictionary<long, List<long>> MergedInto);

    private const int SpiritClass = 10, SorcererClass = 15, ElementalistClass = 16;
    private static readonly HashSet<int> SummonerClasses = [SpiritClass, SorcererClass, ElementalistClass];

    public static Result Merge(IReadOnlyList<MeterLine> lines, bool dropUnmerged = false)
    {
        var identified = lines.Where(l => l.IsIdentified).ToList();
        var totals = identified.ToDictionary(l => l.PlayerId, l => (Damage: l.TotalDamage, Dps: l.DamagePerSecond));
        var merged = new Dictionary<long, List<long>>();
        var kept = new List<MeterLine>();

        foreach (var line in lines.Where(l => !l.IsIdentified))
        {
            // Nur Klassen mit Beschwörungen – sonst würde z. B. ein fremder namenloser Waldläufer
            // deinem Schaden zugeschlagen. Klasse 10 = Geist des Beschwörers (16).
            int ownerClass = line.ClassId == SpiritClass ? ElementalistClass : line.ClassId;
            var owners = SummonerClasses.Contains(line.ClassId)
                ? identified.Where(p => p.ClassId == ownerClass).ToList()
                : [];
            if (owners.Count == 1)
            {
                var owner = owners[0].PlayerId;
                var t = totals[owner];
                totals[owner] = (t.Damage + line.TotalDamage, t.Dps + line.DamagePerSecond);
                if (!merged.TryGetValue(owner, out var ids)) merged[owner] = ids = [];
                ids.Add(line.PlayerId);
            }
            else if (!dropUnmerged || line.IsUser)
            {
                kept.Add(line);
            }
        }

        var result = identified
            .Select(p => p with { TotalDamage = totals[p.PlayerId].Damage, DamagePerSecond = totals[p.PlayerId].Dps })
            .Concat(kept)
            .ToList();

        long sum = result.Sum(l => l.TotalDamage);
        result = result
            .Select(l => l with { DamagePercentage = sum > 0 ? l.TotalDamage * 100.0 / sum : 0 })
            .OrderByDescending(l => l.TotalDamage)
            .ToList();
        return new Result(result, merged);
    }
}
