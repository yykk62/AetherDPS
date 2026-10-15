using System.Collections.ObjectModel;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AetherDPS.App.ViewModels;

/// <summary>Pet in der Liste unter der Radar-Karte; Klick zeigt alle seine Fundorte.</summary>
public sealed partial class RadarRow(int petId, string name, string distance, string level, Bitmap? icon) : ObservableObject
{
    public int PetId { get; } = petId;
    public string Name { get; } = name;
    public string Distance { get; } = distance;
    public string Level { get; } = level;
    public Bitmap? Icon { get; } = icon;
    [ObservableProperty] private bool isSelected;
}

/// <summary>
/// Pet-Radar: kleines Fenster mit den Pets in deiner Nähe, die dir noch fehlen (bzw. nicht auf Höchststufe sind) –
/// Karte um deine Position und die nächsten mit Entfernung und Richtung. Modus „map“: nur solange die Spielkarte offen
/// ist (dann schickt das Spiel jede Sekunde die Feldboss-Liste), „always“: dauerhaft auf Karten mit Pet-Daten, „off“.
/// Die Spielkarte selbst kann der Meter nicht sehen (Zoom/Ausschnitt sind nur im Spiel), daher ein eigener Ausschnitt.
/// </summary>
public sealed partial class PetRadarViewModel : ObservableObject
{
    public const string ModeMap = "map", ModeAlways = "always", ModeOff = "off";
    private readonly Pets pets;
    private readonly MeterSettings settings;
    private readonly DispatcherTimer timer;

    /// <summary>Mit ✕ zugemacht: im Modus „map“ bis zum nächsten Öffnen der Karte, im Modus „always“ bis zum nächsten Kartenwechsel.</summary>
    private bool dismissed;
    private PetMap? lastMap;
    private (double X, double Y)? shownAt;

    public ObservableCollection<RadarRow> Rows { get; } = [];

    [ObservableProperty] private PetMap? map;
    [ObservableProperty] private IReadOnlyList<PetSpawn>? spawns;
    [ObservableProperty] private Point? self;
    [ObservableProperty] private string hint = "";

    /// <summary>Gewähltes Pet: Karte zeigt nur dessen Fundorte, herausgezoomt (wohin teleportieren?).</summary>
    [ObservableProperty] private int? selectedPet;
    [ObservableProperty] private double focusMeters = RadiusMeters;
    [ObservableProperty] private string title = Loc.T("radar.title");

    public const double RadiusMeters = 600;

    /// <summary>Liste unter der Karte auf-/zugeklappt (wird gemerkt).</summary>
    public bool ListOpen
    {
        get => settings.PetRadarList;
        set
        {
            if (settings.PetRadarList == value) return;
            settings.PetRadarList = value;
            settings.Save();
            OnPropertyChanged();
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Select(RadarRow row)
    {
        SelectedPet = SelectedPet == row.PetId ? null : row.PetId;   // nochmal klicken = zurück zum Radar
        Refresh();
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ShowRadar()
    {
        SelectedPet = null;
        Refresh();
    }

    public event EventHandler? ShowRequested;
    public event EventHandler? HideRequested;

    public PetRadarViewModel(Pets pets, MeterSettings settings)
    {
        this.pets = pets;
        this.settings = settings;
        pets.MapOpen += (_, _) => Dispatcher.UIThread.Post(Update);
        pets.PositionChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            Update();
            Moved();
        });
        pets.LevelsChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen) Refresh();
        });
        timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Update());
        timer.Start();
    }

    public bool IsOpen { get; private set; }

    /// <summary>„map“ (Standard), „always“ oder „off“ – Auswahl im Reiter Pets.</summary>
    public string Mode
    {
        get => settings.PetRadarMode;
        set
        {
            if (settings.PetRadarMode == value) return;
            settings.PetRadarMode = value;
            settings.Save();
            dismissed = false;
            OnPropertyChanged();
            Update();
        }
    }

    private bool MapOpenNow => pets.MapOpenSeen is DateTime seen && DateTime.Now - seen <= TimeSpan.FromSeconds(4);

    /// <summary>Karte fürs Radar: die gerade im Spiel geöffnete, sonst die, auf der man steht.</summary>
    private PetMap? Current => pets.ViewedMap is { } viewed && MapOpenNow ? viewed : pets.CurrentMap;

    /// <summary>Zeigen oder verstecken je nach Modus (jede Sekunde und bei neuen Daten).</summary>
    public void Update()
    {
        var current = Current;
        if (current != lastMap)
        {
            lastMap = current;
            if (Mode == ModeAlways) dismissed = false;   // neue Karte → wieder zeigen
        }
        if (Mode == ModeMap && !MapOpenNow) dismissed = false;   // Karte zu → beim nächsten Öffnen wieder zeigen

        bool want = current is not null && !dismissed && Mode switch
        {
            ModeAlways => true,
            ModeMap => MapOpenNow,
            _ => false,
        };
        if (!want)
        {
            Close();
            return;
        }
        bool opening = !IsOpen;
        IsOpen = true;
        if (opening || Map != current) Refresh();
        if (opening) ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>✕: ausblenden, bis die Karte wieder geöffnet wird bzw. man die Karte wechselt.</summary>
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

    /// <summary>Gewünscht laut Filter im Reiter Pets: noch nicht gesammelt, nicht auf Höchststufe oder alle (Stufen unbekannt → alle).</summary>
    private bool Wanted(PetInfo pet) => pets.Level(pet.Id) is not int level || settings.PetFilter switch
    {
        "missing" => level == 0,
        "all" => true,
        _ => level < Pets.TopLevel,
    };

    public void Refresh()
    {
        var current = Current;
        Map = current;
        Rows.Clear();
        if (current is null) return;
        var selected = SelectedPet is int id ? pets.Get(id) : null;
        Spawns = selected is not null ? pets.Spawns(selected, current)
            : pets.All.Where(Wanted).SelectMany(p => pets.Spawns(p, current)).ToList();
        FocusMeters = selected is null ? RadiusMeters : 0;   // gewähltes Pet: alle Fundorte im Blick
        Title = selected is null ? Loc.T("radar.title") : selected.Name;

        bool here = pets.Position is { } pos && pets.CurrentMap == current;
        Self = here ? new Point(pets.Position!.Value.X, pets.Position.Value.Y) : null;
        Hint = here ? "" : Loc.T("radar.no_position");
        if (here) shownAt = (pets.Position!.Value.X, pets.Position.Value.Y);

        // Liste: die nächsten gewünschten Pets (ohne Position: nach Name)
        IEnumerable<(PetInfo Pet, string Distance)> list = here
            ? pets.Nearest(current, Self!.Value.X, Self.Value.Y, Wanted, 5).Select(n => (n.Pet, $"{n.Meters:N0} m {Pets.Compass(n.Bearing)}"))
            : pets.All.Where(p => Wanted(p) && pets.Spawns(p, current).Count > 0).OrderBy(p => p.Name).Take(5).Select(p => (p, ""));
        foreach (var (pet, distance) in list)
        {
            int? level = pets.Level(pet.Id);
            Rows.Add(new RadarRow(pet.Id, pet.Name, distance,
                level is null ? "" : level == 0 ? Loc.T("pets.missing") : Loc.T("profile.level", level), PetIcons.Get(pet.Id))
            { IsSelected = pet.Id == SelectedPet });
        }
        if (selected is not null && Rows.All(r => r.PetId != selected.Id))
            Rows.Insert(0, new RadarRow(selected.Id, selected.Name, "", "", PetIcons.Get(selected.Id)) { IsSelected = true });
    }

    /// <summary>Neue Position: Liste und Ausschnitt neu, sobald man sich ein Stück bewegt hat.</summary>
    private void Moved()
    {
        if (!IsOpen || pets.Position is not { } pos) return;
        if (shownAt is { } at && Math.Abs(at.X - pos.X) + Math.Abs(at.Y - pos.Y) < 500) return;   // unter 5 m
        Refresh();
    }
}
