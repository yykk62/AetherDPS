using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>
/// Hauptfenster (docs/DESIGN.md): vier Orte oben (Jetzt, Kämpfe, Welt, Daevas), Verbindung als
/// aufklappbarer Zustand, Einstellungen als Blatt von rechts, Hinweise als Zeile unter der Leiste.
/// Die Daten und Abläufe (Hinweise, Verbindung, Aktualisierung) stellt weiter <see cref="HubViewModel"/>.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    public const int PlaceNow = 0, PlaceFights = 1, PlaceWorld = 2, PlaceDaevas = 3;
    public const int WorldTimers = 0, WorldChecklist = 1, WorldPets = 2;
    public const int DaevasMe = 0, DaevasParty = 1;

    public HubViewModel Hub { get; }
    public MainViewModel Overlay => Hub.Overlay;
    public NowViewModel Now { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNow), nameof(IsFights), nameof(IsWorld), nameof(IsDaevas))]
    private int place;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTimers), nameof(IsChecklist), nameof(IsPets))]
    private int worldSection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMe), nameof(IsParty))]
    private int daevasSection;

    [ObservableProperty] private bool settingsOpen;
    [ObservableProperty] private bool connectionOpen;
    [ObservableProperty] private bool gameRunning = true;
    [ObservableProperty] private string gameStatus = "";
    [ObservableProperty] private string connectionStatus = "";

    public bool IsNow => Place == PlaceNow;
    public bool IsFights => Place == PlaceFights;
    public bool IsWorld => Place == PlaceWorld;
    public bool IsDaevas => Place == PlaceDaevas;
    public bool IsTimers => WorldSection == WorldTimers;
    public bool IsChecklist => WorldSection == WorldChecklist;
    public bool IsPets => WorldSection == WorldPets;
    public bool IsMe => DaevasSection == DaevasMe;
    public bool IsParty => DaevasSection == DaevasParty;

    public ShellViewModel(HubViewModel hub, NowViewModel now)
    {
        Hub = hub;
        Now = now;
        hub.Refreshed += (_, _) => Refresh();
        hub.Party.ProfileRequested += (_, _) => { Place = PlaceDaevas; DaevasSection = DaevasMe; };
        SyncHubTab();
    }

    partial void OnPlaceChanged(int value) => SyncHubTab();
    partial void OnWorldSectionChanged(int value) => SyncHubTab();
    partial void OnDaevasSectionChanged(int value) => SyncHubTab();
    partial void OnConnectionOpenChanged(bool value)
    {
        if (value) SettingsOpen = false;
        SyncHubTab();
    }
    partial void OnSettingsOpenChanged(bool value)
    {
        if (value) ConnectionOpen = false;
    }

    /// <summary>Der Hub aktualisiert jede Sekunde nur den Teil, der gerade zu sehen ist.</summary>
    private void SyncHubTab() => Hub.SelectedTab = ConnectionOpen ? HubViewModel.TabConnection : Place switch
    {
        PlaceFights => HubViewModel.TabRuns,
        PlaceWorld => WorldSection switch { WorldChecklist => HubViewModel.TabChecklist, WorldPets => HubViewModel.TabPets, _ => HubViewModel.TabTimers },
        PlaceDaevas => DaevasSection == DaevasParty ? HubViewModel.TabParty : HubViewModel.TabProfile,
        _ => HubViewModel.TabDps,
    };

    /// <summary>Alte Reiter-Nummern (Tray, Overlay, Timer-Pills, Gruppenbeitritt) auf Orte abbilden.</summary>
    public void Show(int legacyTab)
    {
        SettingsOpen = false;
        ConnectionOpen = false;
        switch (legacyTab)
        {
            case HubViewModel.TabRuns: Place = PlaceFights; break;
            case HubViewModel.TabParty: Place = PlaceDaevas; DaevasSection = DaevasParty; break;
            case HubViewModel.TabProfile: Place = PlaceDaevas; DaevasSection = DaevasMe; break;
            case HubViewModel.TabPets: Place = PlaceWorld; WorldSection = WorldPets; break;
            case HubViewModel.TabTimers: Place = PlaceWorld; WorldSection = WorldTimers; break;
            case HubViewModel.TabChecklist: Place = PlaceWorld; WorldSection = WorldChecklist; break;
            case HubViewModel.TabConnection: ConnectionOpen = true; break;
            case HubViewModel.TabSettings: SettingsOpen = true; break;
            default: Place = PlaceNow; break;
        }
    }

    [RelayCommand]
    private void Go(string? target)
    {
        if (int.TryParse(target, out var p) && p is >= PlaceNow and <= PlaceDaevas) Place = p;
    }

    [RelayCommand]
    private void GoWorld(string? section)
    {
        if (int.TryParse(section, out var s)) WorldSection = s;
    }

    [RelayCommand]
    private void GoDaevas(string? section)
    {
        if (int.TryParse(section, out var s)) DaevasSection = s;
    }

    [RelayCommand]
    private void ToggleSettings() => SettingsOpen = !SettingsOpen;

    [RelayCommand]
    private void ToggleConnection() => ConnectionOpen = !ConnectionOpen;

    [RelayCommand]
    private void CloseSheets()
    {
        SettingsOpen = false;
        ConnectionOpen = false;
    }

    /// <summary>Statusfeld „AION 2 ist aus“: Klick startet das Spiel.</summary>
    [RelayCommand]
    private void StartGame()
    {
        if (!GameRunning) Hub.LaunchGameCommand.Execute(null);
    }

    public void Refresh()
    {
        GameRunning = Hub.GameRunning;
        GameStatus = Loc.T(GameRunning ? "shell.status.game_on" : "shell.status.game_off");
        // nie ein Icon ohne Wort: solange der Mitschnitt noch keinen Zustand meldet, „Suche Verbindung“
        ConnectionStatus = string.IsNullOrWhiteSpace(Overlay.StatusText) ? Loc.T("shell.status.searching") : Overlay.StatusText;
        Now.Refresh(full: IsNow);
    }
}
