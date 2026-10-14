using AionDpsMeter.Services.Services.Entity;

namespace AetherDPS.Engine;

/// <summary>
/// Gehört ein Spieler zu „uns“ – du selbst oder deine Gruppe? Das Spiel schickt auch die Kämpfe
/// aller Spieler in der Nähe. Boss-Karte und Auto-Modus sollen nur auf eigene Kämpfe reagieren,
/// sonst springen sie in der offenen Welt von Mob zu Mob (Rückmeldung 14.10.: Wassergeister).
/// </summary>
public sealed class OwnSide(SelfIdentity self, PartyRoster roster, EntityTracker entities, MeterSettings settings)
{
    public bool IsOwn(int entityId)
    {
        // „Nur meine Gruppe“ aus: dann zählt jeder (wie in der Spielerliste)
        if (!settings.PartyOnly) return true;
        if (self.SelfId == entityId) return true;
        var player = entities.GetPlayerEntity(entityId);
        if (player is null) return false;
        if (player.IsUser) return true;
        return roster.Count > 0 && roster.Members.Any(m => m.Name == player.Name);
    }
}
