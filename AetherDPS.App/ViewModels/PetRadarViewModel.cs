using System.Collections.ObjectModel;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AetherDPS.App.ViewModels;

public sealed record RadarRow(string Name, string Distance, string Level);

/// <summary>
/// Pet-Radar: Solange die Spielkarte offen ist, zeigt ein kleines Fenster die Pets in deiner Nähe, die dir noch
/// fehlen (bzw. nicht auf Höchststufe sind) – Karte um deine Position und die nächsten mit Entfernung und Richtung.
/// Die Spielkarte selbst kann der Meter nicht sehen (Zoom/Ausschnitt sind nur im Spiel), daher ein eigener Ausschnitt.
/// </summary>
public sealed partial class PetRadarViewModel : ObservableObject
{
    public const double RadiusMeters = 900;
    private readonly Pets pets;
    private readonly MeterSettings settings;
    private readonly DispatcherTimer closeTimer;

    public ObservableCollection<RadarRow> Rows { get; } = [];

    [ObservableProperty] private PetMap? map;
    [ObservableProperty] private IReadOnlyList<PetSpawn>? spawns;
    [ObservableProperty] private Point? self;
    [ObservableProperty] private string hint = "";

    public event EventHandler? ShowRequested;
    public event EventHandler? HideRequested;

    public PetRadarViewModel(Pets pets, MeterSettings settings)
    {
        this.pets = pets;
        this.settings = settings;
        pets.MapOpen += (_, _) => Dispatcher.UIThread.Post(OnMapOpen);
        pets.PositionChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen) Refresh();
        });
        // Karte zu = die Feldboss-Liste bleibt aus
        closeTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            if (pets.MapOpenSeen is DateTime seen && DateTime.Now - seen <= TimeSpan.FromSeconds(4)) return;
            dismissed = false;
            closeTimer!.Stop();
            Close();
        });
    }

    public bool IsOpen { get; private set; }

    /// <summary>Mit ✕ zugemacht: erst beim nächsten Öffnen der Spielkarte wieder zeigen.</summary>
    private bool dismissed;

    private void OnMapOpen()
    {
        closeTimer.Start();
        if (!settings.PetRadar || IsOpen || dismissed) return;
        if (pets.CurrentMap is null) return;   // Dungeon oder Karte ohne Pet-Daten
        IsOpen = true;
        Refresh();
        ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>✕: bis die Spielkarte zu ist, nicht wieder aufgehen.</summary>
    public void Dismiss()
    {
        dismissed = true;
        Close();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Gewünscht: fehlt noch oder ist nicht auf Höchststufe (Stufen unbekannt → alle).</summary>
    private bool Wanted(PetInfo pet) => pets.Level(pet.Id) is not int level || level < Pets.TopLevel;

    public void Refresh()
    {
        var current = pets.CurrentMap;
        Map = current;
        Rows.Clear();
        if (current is null) return;
        Spawns = pets.All.Where(Wanted).SelectMany(p => pets.Spawns(p, current)).ToList();
        if (pets.Position is { } pos && pos.MapId != 0 && pets.CurrentMap == current)
        {
            Self = new Point(pos.X, pos.Y);
            Hint = "";
            foreach (var n in pets.Nearest(current, pos.X, pos.Y, Wanted, 6))
            {
                int? level = pets.Level(n.Pet.Id);
                Rows.Add(new RadarRow(n.Pet.Name, $"{n.Meters:N0} m {Pets.Compass(n.Bearing)}",
                    level is null ? "" : level == 0 ? Loc.T("pets.missing") : Loc.T("profile.level", level)));
            }
        }
        else
        {
            Self = null;
            Hint = Loc.T("radar.no_position");
        }
    }
}
