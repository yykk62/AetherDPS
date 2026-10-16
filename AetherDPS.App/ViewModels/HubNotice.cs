using Avalonia.Data.Converters;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>
/// Hinweiskarte oben im Hauptfenster (Zustimmung zum Zähler, Absturz, Was ist neu, Nachricht an alle,
/// Spiel-Update erkannt). Bis zu zwei Knöpfe; ohne Knöpfe gibt es einen Schließen-Knopf.
/// Die Titelzeile ist immer zu sehen, der Rest lässt sich auf- und zuklappen (lange Texte wie „Was ist neu“
/// starten zugeklappt, damit sie das Fenster nicht füllen).
/// </summary>
public sealed partial class HubNotice : ObservableObject
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

    /// <summary>Text, Detail und Knöpfe sichtbar. Karten mit Knöpfen (Fragen) starten offen.</summary>
    [ObservableProperty] private bool isExpanded = true;

    public bool HasPrimary => Primary is not null;
    public bool HasSecondary => Secondary is not null;
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);
    public bool CanClose => Primary is null && Secondary is null;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

/// <summary>Konverter für die Hinweiskarte: Pfeil und Tooltip je nach Zustand.</summary>
public static class NoticeConv
{
    public static readonly IValueConverter ChevronUpElseDown =
        new FuncValueConverter<bool, Avalonia.Media.Imaging.Bitmap?>(open => Icons.Get(open ? "chevron-up" : "chevron-down", 12));

    public static readonly IValueConverter CollapseElseExpandTip =
        new FuncValueConverter<bool, string>(open => Loc.T(open ? "notice.collapse" : "notice.expand"));
}
