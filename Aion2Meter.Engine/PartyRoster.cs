using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.Services.Entity;

namespace Aion2Meter.Engine;

/// <summary>
/// Merkt sich die aktuelle Gruppe (Gruppen-Paket 0x9702) für den Filter
/// „nur meine Gruppe“. Im Zweifel wird ein Spieler gezeigt statt versteckt.
/// </summary>
public sealed class PartyRoster
{
    private readonly Lock gate = new();
    private HashSet<string> names = new(StringComparer.Ordinal);

    public PartyRoster(EntityTracker entities)
    {
        entities.PartyUpdated += OnPartyUpdated;
    }

    public int Count { get { lock (gate) return names.Count; } }

    private void OnPartyUpdated(IReadOnlyList<Player> members)
    {
        var updated = new HashSet<string>(members.Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n)),
            StringComparer.Ordinal);
        lock (gate) names = updated;
    }

    /// <summary>
    /// Zeigen? Ohne bekannte Gruppe (solo) wird nicht gefiltert. Der eigene Charakter
    /// und noch namenlose Spieler bleiben immer sichtbar – lieber einer zu viel als
    /// ein Gruppenmitglied, dessen Name noch nicht angekommen ist, zu verstecken.
    /// </summary>
    public bool IsVisible(string name, bool isUser, bool isIdentified)
    {
        lock (gate)
        {
            if (names.Count == 0 || isUser || !isIdentified) return true;
            return names.Contains(name);
        }
    }
}
