using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>
/// Hinweiskarte oben im Hauptfenster (Zustimmung zum Zähler, Absturz, Was ist neu, Nachricht an alle,
/// Spiel-Update erkannt). Bis zu zwei Knöpfe; ohne Knöpfe gibt es einen Schließen-Knopf.
/// </summary>
public sealed class HubNotice
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Text { get; init; }

    /// <summary>Kleiner Kasten mit Beispieldaten o. Ä. (Festbreitenschrift).</summary>
    public string? Detail { get; init; }

    public bool IsWarning { get; init; }

    /// <summary>Icon-Name aus dem eigenen Set (Assets/icons), z. B. "download", "bug", "megaphone".</summary>
    public string Icon { get; init; } = "info";

    public string? PrimaryText { get; init; }
    public IRelayCommand? Primary { get; init; }
    public string? SecondaryText { get; init; }
    public IRelayCommand? Secondary { get; init; }

    /// <summary>Wird vom Hauptfenster gesetzt: Karte entfernen.</summary>
    public IRelayCommand? Close { get; set; }

    public bool HasPrimary => Primary is not null;
    public bool HasSecondary => Secondary is not null;
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);
    public bool CanClose => Primary is null && Secondary is null;
}
