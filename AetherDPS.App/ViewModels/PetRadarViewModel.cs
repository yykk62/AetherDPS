using System.Collections.ObjectModel;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AetherDPS.App.ViewModels;

/// <summary>Pet in der Liste unter der Radar-Karte; Klick zeigt alle seine Fundorte.</summary>
public sealed partial class RadarRow(int petId, string name, string distance, string level, Bitmap? icon, Avalonia.Media.IBrush? color = null, string monster = "") : ObservableObject
{
    /// <summary>Monster am nächsten Fundort (die Seele heißt oft anders als das Monster, z. B. Bienen-Abart ← Schwarzmusterbiene).</summary>
    public string Monster { get; } = monster;
    public bool HasMonster => Monster.Length > 0;
    /// <summary>Farbe seiner Punkte auf der Karte (nur die ersten fünf), sonst null.</summary>
    public Avalonia.Media.IBrush? Color { get; } = color;
    public bool HasColor => Color is not null;
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
    [ObservableProperty] private IReadOnlyDictionary<int, Avalonia.Media.IBrush>? spawnBrushes;

    /// <summary>So viele Pets stehen in der Liste (scrollbar, sichtbar sind fünf).</summary>
    public const int ListSize = 30;

    /// <summary>Gewähltes Pet: Karte zeigt nur dessen Fundorte (Zoom bleibt; liegt keiner im Bild, so weit raus, dass der nächste zu sehen ist).</summary>
    [ObservableProperty] private int? selectedPet;

    /// <summary>Standard-Umkreis der Minimap in Metern, bis man selbst zoomt (dann gemerkt in PetRadarMeters).</summary>
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
            FollowSelf();
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
        if (sample) return;
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
    private bool Wanted(PetInfo pet) =>
        (settings.PetSpecies is not { } species || pet.Species == species)
        && (pets.Level(pet.Id) is not int level || settings.PetFilter switch
        {
            "missing" => level == 0,
            "all" => true,
            _ => level < Pets.TopLevel,
        });

    private bool sample;

    /// <summary>
    /// Platzierungsmodus: Radar mit Beispieldaten – die Karte, auf der du stehst (sonst die erste mit Pets), du mitten
    /// zwischen Fundorten, die Liste dazu. Aus = wieder die echte Lage.
    /// </summary>
    public void ShowSample(bool on)
    {
        if (sample == on) return;
        sample = on;
        if (!on)
        {
            Refresh();
            Update();
            return;
        }
        var map = Current ?? pets.Maps.FirstOrDefault(m => pets.All.Any(p => pets.Spawns(p, m).Count > 0));
        if (map is null) return;
        var spawn = pets.All.SelectMany(p => pets.Spawns(p, map)).FirstOrDefault();
        Refresh(map, spawn is null ? new Point(0, 0) : new Point(spawn.X + 4_000, spawn.Y + 3_000));
    }

    public void Refresh()
    {
        if (sample) return;
        Refresh(null, null);
    }

    private void Refresh(PetMap? sampleMap, Point? sampleSelf)
    {
        var current = sampleMap ?? Current;
        Map = current;
        Rows.Clear();
        if (current is null)
        {
            Self = null;   // auch nach den Beispieldaten des Platzierungsmodus
            Spawns = null;
            return;
        }
        var selected = SelectedPet is int id ? pets.Get(id) : null;
        Point? at = sampleSelf ?? (pets.Position is { } pos && pets.CurrentMap == current ? new Point(pos.X, pos.Y) : null);
        bool here = at is not null;
        Self = at;
        Hint = here ? "" : Loc.T(pets.KnowsSelf ? "radar.no_position" : "radar.no_self");
        if (here && sampleSelf is null) shownAt = (at!.Value.X, at.Value.Y);

        // Liste: die nächsten gewünschten Pets (ohne Position: nach Name), scrollbar
        var list = here
            ? pets.Nearest(current, Self!.Value.X, Self.Value.Y, Wanted, ListSize)
                .Select(n => (n.Pet, Distance: $"{n.Meters:N0} m {Pets.Compass(n.Bearing)}", Monster: GameText.NpcName(n.Spawn.Npc) ?? "")).ToList()
            : pets.All.Where(p => Wanted(p) && pets.Spawns(p, current).Count > 0).OrderBy(p => p.Name).Take(ListSize)
                .Select(p => (Pet: p, Distance: "", Monster: "")).ToList();
        if (selected is not null && list.All(l => l.Pet.Id != selected.Id)) list.Insert(0, (selected, "", ""));

        // Karte: gewähltes Pet allein, sonst höchstens fünf. Ohne Gattungs-Filter das nächste je Gattung in der Farbe
        // der Gattung, mit Filter die fünf nächsten dieser Gattung in unterschiedlichen Farben.
        bool oneSpecies = settings.PetSpecies is not null;
        var shown = new List<(PetInfo Pet, Avalonia.Media.IBrush Brush)>();
        if (selected is not null) shown.Add((selected, oneSpecies ? PetColors.Palette[0] : PetColors.ForSpecies(selected.Species)));
        else if (oneSpecies)
            shown.AddRange(list.Take(PetColors.Palette.Length).Select((l, i) => (l.Pet, PetColors.Palette[i])));
        else
            shown.AddRange(Pets.SpeciesOrder
                .Select(sp => list.Select(l => l.Pet).FirstOrDefault(p => p.Species == sp))
                .Where(p => p is not null)
                .Select(p => (p!, PetColors.ForSpecies(p!.Species))));
        var brushes = new Dictionary<int, Avalonia.Media.IBrush>();
        var colorOf = new Dictionary<int, Avalonia.Media.IBrush>();
        foreach (var (pet, brush) in shown)
        {
            colorOf[pet.Id] = brush;
            foreach (var npc in pet.Npcs) brushes[npc] = brush;
        }
        SpawnBrushes = brushes;
        Spawns = shown.SelectMany(s => pets.Spawns(s.Pet, current)).ToList();

        foreach (var (pet, distance, monster) in list)
        {
            int? level = pets.Level(pet.Id);
            Rows.Add(new RadarRow(pet.Id, pet.Name, distance,
                level is null ? "" : level == 0 ? Loc.T("pets.missing") : Loc.T("profile.level", level), PetIcons.Get(pet.Id), colorOf.GetValueOrDefault(pet.Id),
                monster == pet.Name ? "" : monster)
            { IsSelected = pet.Id == SelectedPet });
        }
    }

    /// <summary>Jede neue Position: der Punkt (und mit ihm die Minimap) wandert sofort mit.</summary>
    private void FollowSelf()
    {
        if (sample || !IsOpen || Map is null || pets.Position is not { } pos || pets.CurrentMap != Map) return;
        Self = new Point(pos.X, pos.Y);
    }

    /// <summary>Neue Position: Liste neu, sobald man sich ein Stück bewegt hat.</summary>
    private void Moved()
    {
        if (sample || !IsOpen || pets.Position is not { } pos) return;
        if (shownAt is { } at && Math.Abs(at.X - pos.X) + Math.Abs(at.Y - pos.Y) < 1500) return;   // unter 15 m
        Refresh();
    }
}
