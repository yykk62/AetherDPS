namespace Aion2Meter.Engine;

/// <summary>
/// Wer erscheint in der Liste? (Overlay, Hauptfenster und Archiv nutzen dieselbe Regel.)
/// „Nur meine Gruppe“:
///   in einer Gruppe → nur die Gruppe (+ zugeordnete Beschwörungen),
///   solo           → nur du (+ deine Beschwörungen) – Open World zeigte sonst alle in der Nähe,
///   solo und dein Charakter noch unbekannt → alles (sonst wäre die Liste leer).
/// </summary>
public static class MeterFilter
{
    public static SummonMerger.Result Apply(IReadOnlyList<MeterLine> lines, PartyRoster roster, MeterSettings settings)
    {
        if (!settings.PartyOnly) return SummonMerger.Merge(lines);

        if (roster.Count > 0)
        {
            var party = lines.Where(l => roster.IsVisible(l.PlayerName, l.IsUser, l.IsIdentified)).ToList();
            return SummonMerger.Merge(party, dropUnmerged: true);
        }

        if (!lines.Any(l => l.IsUser)) return SummonMerger.Merge(lines);

        var solo = lines.Where(l => l.IsUser || !l.IsIdentified).ToList();
        return SummonMerger.Merge(solo, dropUnmerged: true);
    }
}
