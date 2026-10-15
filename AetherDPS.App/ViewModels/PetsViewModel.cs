using System.Collections.ObjectModel;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

public sealed record PetMapOption(PetMap Map, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Gattung als Bild-Knopf: Porträt eines typischen Pets, Rahmen in der Farbe der Gattung („alle“: ohne Bild).</summary>
public sealed record SpeciesOption(string? Key, string Name, Avalonia.Media.Imaging.Bitmap? Icon = null)
{
    public IBrush Brush => Key is null ? Brushes.Transparent : Services.PetColors.ForSpecies(Key);
    public bool IsAll => Key is null;
    public override string ToString() => Name;
}

/// <summary>Filter nach eigenem Stand: alle, noch nicht gesammelt (Stufe 0), noch nicht auf Höchststufe.</summary>
public sealed record PetFilterOption(string Code, string Name, string? Short = null)
{
    /// <summary>Kurzer Text für den Knopf (der lange steht im Tooltip).</summary>
    public string Label => Short ?? Name;
    public override string ToString() => Name;
}

/// <summary>Zeile der Pet-Liste: Name, Gattung, eigene Stufe, Zahl der Spawnpunkte auf der gewählten Karte.</summary>
public sealed record PetRow(PetInfo Pet, string Name, string Species, string LevelText, IBrush LevelBrush, int Spawns, bool Done)
{
    public IBrush SpeciesBrush => Services.PetColors.ForSpecies(Pet.Species);
    public Avalonia.Media.Imaging.Bitmap? Icon => Services.PetIcons.Get(Pet.Id);
}

/// <summary>Monster eines Pets mit seinen Spawnpunkten auf der Karte.</summary>
public sealed record PetMonsterRow(string Name, string Count, int Number = 0);

/// <summary>
/// Reiter „Pets“: alle Pets, deren Monster auf einer Weltkarte spawnen, mit deiner Stufe (aus der Pet-Liste beim Login)
/// und einer Karte mit allen Spawnpunkten und deiner letzten Position.
/// </summary>
public sealed partial class PetsViewModel : ObservableObject
{
    private static readonly IBrush Missing = new SolidColorBrush(Color.Parse("#E86A6A"));
    private static readonly IBrush Partial = new SolidColorBrush(Color.Parse("#E0A84A"));
    private static readonly IBrush Max = new SolidColorBrush(Color.Parse("#4CC36A"));
    private static readonly IBrush Unknown = new SolidColorBrush(Color.Parse("#8E96A3"));

    private readonly Pets pets;
    private readonly MeterSettings settings;
    private bool built;

    public IReadOnlyList<PetMapOption> MapOptions { get; }
    public IReadOnlyList<SpeciesOption> SpeciesOptions { get; }
    public IReadOnlyList<PetFilterOption> FilterOptions { get; } =
    [
        new("all", Loc.T("pets.filter_all"), Loc.T("pets.filter_short.all")),
        new("missing", Loc.T("pets.filter_missing"), Loc.T("pets.filter_short.missing")),
        new("open", Loc.T("pets.filter_open"), Loc.T("pets.filter_short.open")),
    ];

    /// <summary>Mehr als eine Karte zur Auswahl? (Elyos: Verteron + Abyss, Asmodier: Altgard + Abyss)</summary>
    public bool HasMapChoice => MapOptions.Count > 1;
    public ObservableCollection<PetRow> Rows { get; } = [];
    public ObservableCollection<PetMonsterRow> Monsters { get; } = [];

    [ObservableProperty] private PetMapOption? selectedMap;
    [ObservableProperty] private SpeciesOption? selectedSpecies;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private PetFilterOption selectedFilter;
    [ObservableProperty] private PetRow? selected;

    [ObservableProperty] private PetMap? map;
    [ObservableProperty] private IReadOnlyList<PetSpawn>? spawns;
    [ObservableProperty] private Point? self;
    [ObservableProperty] private string summary = "";
    [ObservableProperty] private string levelsHint = "";
    [ObservableProperty] private string detailTitle = "";
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? detailIcon;
    [ObservableProperty] private IBrush dotBrush = Services.PetColors.Palette[0];
    [ObservableProperty] private string detailLevel = "";
    [ObservableProperty] private string detailRegions = "";
    [ObservableProperty] private string positionText = "";

    public PetsViewModel(Pets pets, MeterSettings settings)
    {
        this.pets = pets;
        this.settings = settings;
        // Nur die Karten der eigenen Seite (Server 13xx = Elyos, 23xx = Asmodier), die Abyss teilen sich beide
        var server = Engine.Community.Armory.ServerId(settings.CharacterServer);
        string? otherSide = server switch { >= 2000 => "verteron", >= 1000 => "altgard", _ => null };
        MapOptions = pets.Maps.Where(m => m.Key != otherSide)
            .Select(m => new PetMapOption(m, Loc.T("pets.map." + m.Key).Split(" (")[0])).ToList();
        SpeciesOptions = new[] { new SpeciesOption(null, Loc.T("pets.species_all")) }
            .Concat(Pets.SpeciesOrder.Select(s => new SpeciesOption(s, Loc.T("pets.species." + s), SpeciesIcon(pets, s)))).ToList();
        selectedSpecies = SpeciesOptions.FirstOrDefault(s => s.Key == settings.PetSpecies) ?? SpeciesOptions[0];
        selectedFilter = FilterOptions.FirstOrDefault(f => f.Code == settings.PetFilter) ?? FilterOptions[0];
        selectedMap = DefaultMap();
        pets.LevelsChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (built) Rebuild();
        });
        pets.PositionChanged += (_, _) => Dispatcher.UIThread.Post(UpdatePosition);
    }

    /// <summary>Bild einer Gattung: typische Vertreter, sonst das erste Pet der Gattung mit Porträt.</summary>
    private static Avalonia.Media.Imaging.Bitmap? SpeciesIcon(Pets pets, string species)
    {
        int[] preferred = species switch
        {
            "Intellect" => [1002], "Feral" => [1004], "Nature" => [1003], "Special" => [1001], _ => [],
        };
        foreach (var id in preferred.Concat(pets.All.Where(p => p.Species == species).Select(p => p.Id)))
            if (Services.PetIcons.Get(id) is { } icon && pets.Get(id)?.Species == species) return icon;
        return null;
    }

    /// <summary>Pet-Radar: aus, solange die Spielkarte offen ist, oder immer.</summary>
    public IReadOnlyList<PetFilterOption> RadarModes { get; } =
        [new(PetRadarViewModel.ModeMap, Loc.T("radar.mode_map")), new(PetRadarViewModel.ModeAlways, Loc.T("radar.mode_always")), new(PetRadarViewModel.ModeOff, Loc.T("radar.mode_off"))];

    public PetFilterOption SelectedRadarMode
    {
        get => RadarModes.FirstOrDefault(m => m.Code == settings.PetRadarMode) ?? RadarModes[0];
        set
        {
            if (value is null || settings.PetRadarMode == value.Code) return;
            settings.PetRadarMode = value.Code;
            settings.Save();
            OnPropertyChanged();
        }
    }

    /// <summary>Beim ersten Öffnen aufbauen (Karte und Liste).</summary>
    public void Refresh()
    {
        if (!built)
        {
            built = true;
            Rebuild();
        }
        UpdatePosition();
    }

    private PetMapOption? DefaultMap()
    {
        var current = pets.CurrentMap;
        if (current is not null) return MapOptions.FirstOrDefault(o => o.Map == current);
        var server = Engine.Community.Armory.ServerId(settings.CharacterServer);
        var key = server is >= 2000 ? "altgard" : "verteron";
        return MapOptions.FirstOrDefault(o => o.Map.Key == key) ?? MapOptions.FirstOrDefault();
    }

    partial void OnSelectedMapChanged(PetMapOption? value) => Rebuild();
    partial void OnSelectedSpeciesChanged(SpeciesOption? value)
    {
        settings.PetSpecies = value?.Key;   // gilt auch fürs Pet-Radar
        settings.Save();
        Rebuild();
    }
    partial void OnSearchChanged(string value) => Rebuild();
    partial void OnSelectedFilterChanged(PetFilterOption value)
    {
        settings.PetFilter = value.Code;   // gilt auch fürs Pet-Radar
        settings.Save();
        Rebuild();
    }
    partial void OnSelectedChanged(PetRow? value) => ShowSelected();

    private void Rebuild()
    {
        if (!built || SelectedMap is not { } option) return;
        Map = option.Map;
        int? keep = Selected?.Pet.Id;
        var rows = new List<PetRow>();
        int total = 0, done = 0;
        foreach (var pet in pets.All)
        {
            int count = pets.Spawns(pet, option.Map).Count;
            if (count == 0) continue;
            total++;
            var row = Row(pet, count);
            if (row.Done) done++;
            if (SelectedSpecies?.Key is { } species && pet.Species != species) continue;
            if (SelectedFilter.Code == "open" && row.Done) continue;
            if (SelectedFilter.Code == "missing" && pets.Level(pet.Id) is > 0) continue;
            if (Search.Trim() is { Length: > 0 } term && !pet.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                && !pet.Npcs.Any(n => (GameText.NpcName(n) ?? "").Contains(term, StringComparison.CurrentCultureIgnoreCase))) continue;
            rows.Add(row);
        }
        Rows.Clear();
        foreach (var row in rows.OrderBy(r => r.Done).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)) Rows.Add(row);
        Summary = pets.KnowsLevels ? Loc.T("pets.summary", done, total, option.Name) : Loc.T("pets.summary_unknown", total, option.Name);
        LevelsHint = pets.KnowsLevels ? "" : Loc.T("pets.levels_hint");
        Selected = Rows.FirstOrDefault(r => r.Pet.Id == keep) ?? Rows.FirstOrDefault();
        ShowSelected();
        UpdatePosition();
    }

    private PetRow Row(PetInfo pet, int spawns)
    {
        string species = Loc.T("pets.species." + pet.Species);
        int? level = pets.Level(pet.Id);
        if (level is null) return new PetRow(pet, pet.Name, species, "?", Unknown, spawns, false);
        if (level == 0) return new PetRow(pet, pet.Name, species, Loc.T("pets.missing"), Missing, spawns, false);
        if (level >= Pets.TopLevel) return new PetRow(pet, pet.Name, species, Loc.T("pets.level_max", level), Max, spawns, true);
        return new PetRow(pet, pet.Name, species, Loc.T("pets.level", level, pets.Progress(pet.Id), Pets.Needed(level.Value)), Partial, spawns, false);
    }

    private void ShowSelected()
    {
        Monsters.Clear();
        if (Selected is not { } row || SelectedMap is not { } option)
        {
            Spawns = [];
            DetailTitle = "";
            DetailIcon = null;
            DetailLevel = "";
            DetailRegions = "";
            return;
        }
        var spawns = pets.Spawns(row.Pet, option.Map);
        Spawns = spawns;
        DetailTitle = $"{row.Name} · {row.Species}";
        DetailIcon = row.Icon;
        DotBrush = row.SpeciesBrush;
        DetailLevel = row.LevelText == "?" ? Loc.T("pets.level_unknown") : row.LevelText;
        var regions = pets.Regions(row.Pet, option.Map);
        DetailRegions = regions.Count > 0 ? string.Join(", ", regions) : "";
        foreach (var g in spawns.GroupBy(s => s.Npc).OrderByDescending(g => g.Count()))
            Monsters.Add(new PetMonsterRow(GameText.NpcName(g.Key) ?? $"#{g.Key}", Loc.T("pets.spawn_count", g.Count()), g.Count()));
    }

    private void UpdatePosition()
    {
        if (pets.Position is { } pos && Map is { } map && pets.CurrentMap == map)
        {
            Self = new Point(pos.X, pos.Y);
            PositionText = Loc.T("pets.position", pos.At);
        }
        else
        {
            Self = null;
            PositionText = Loc.T("pets.position_none");
        }
    }

    [RelayCommand]
    private void ShowAll() => Search = "";
}
