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

public sealed record SpeciesOption(string? Key, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Zeile der Pet-Liste: Name, Gattung, eigene Stufe, Zahl der Spawnpunkte auf der gewählten Karte.</summary>
public sealed record PetRow(PetInfo Pet, string Name, string Species, string LevelText, IBrush LevelBrush, int Spawns, bool Done);

/// <summary>Monster eines Pets mit seinen Spawnpunkten auf der Karte.</summary>
public sealed record PetMonsterRow(string Name, string Count);

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
    public ObservableCollection<PetRow> Rows { get; } = [];
    public ObservableCollection<PetMonsterRow> Monsters { get; } = [];

    [ObservableProperty] private PetMapOption? selectedMap;
    [ObservableProperty] private SpeciesOption? selectedSpecies;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private bool onlyOpen;
    [ObservableProperty] private PetRow? selected;

    [ObservableProperty] private PetMap? map;
    [ObservableProperty] private IReadOnlyList<PetSpawn>? spawns;
    [ObservableProperty] private Point? self;
    [ObservableProperty] private string summary = "";
    [ObservableProperty] private string levelsHint = "";
    [ObservableProperty] private string detailTitle = "";
    [ObservableProperty] private string detailLevel = "";
    [ObservableProperty] private string detailRegions = "";
    [ObservableProperty] private string positionText = "";

    public PetsViewModel(Pets pets, MeterSettings settings)
    {
        this.pets = pets;
        this.settings = settings;
        MapOptions = pets.Maps.Select(m => new PetMapOption(m, Loc.T("pets.map." + m.Key))).ToList();
        SpeciesOptions = new[] { new SpeciesOption(null, Loc.T("pets.species_all")) }
            .Concat(Pets.SpeciesOrder.Select(s => new SpeciesOption(s, Loc.T("pets.species." + s)))).ToList();
        selectedSpecies = SpeciesOptions[0];
        selectedMap = DefaultMap();
        pets.LevelsChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (built) Rebuild();
        });
        pets.PositionChanged += (_, _) => Dispatcher.UIThread.Post(UpdatePosition);
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
    partial void OnSelectedSpeciesChanged(SpeciesOption? value) => Rebuild();
    partial void OnSearchChanged(string value) => Rebuild();
    partial void OnOnlyOpenChanged(bool value) => Rebuild();
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
            if (OnlyOpen && row.Done) continue;
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
            DetailLevel = "";
            DetailRegions = "";
            return;
        }
        var spawns = pets.Spawns(row.Pet, option.Map);
        Spawns = spawns;
        DetailTitle = $"{row.Name} · {row.Species}";
        DetailLevel = row.LevelText == "?" ? Loc.T("pets.level_unknown") : row.LevelText;
        var regions = pets.Regions(row.Pet, option.Map);
        DetailRegions = regions.Count > 0 ? string.Join(", ", regions) : "";
        foreach (var g in spawns.GroupBy(s => s.Npc).OrderByDescending(g => g.Count()))
            Monsters.Add(new PetMonsterRow(GameText.NpcName(g.Key) ?? $"#{g.Key}", Loc.T("pets.spawn_count", g.Count())));
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
