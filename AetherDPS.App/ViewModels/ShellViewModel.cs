using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>
/// Neues Hauptfenster (docs/DESIGN.md): vier Orte oben (Jetzt, Kämpfe, Welt, Daevas), Zustand der
/// Verbindung als Statusfeld, Meter-Schalter fest rechts. Noch im Aufbau: bisher ist „Jetzt“ fertig.
/// </summary>
public sealed partial class ShellViewModel(MainViewModel overlay, NowViewModel now) : ObservableObject
{
    public const int PlaceNow = 0, PlaceFights = 1, PlaceWorld = 2, PlaceDaevas = 3;

    public MainViewModel Overlay { get; } = overlay;
    public NowViewModel Now { get; } = now;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNow), nameof(IsFights), nameof(IsWorld), nameof(IsDaevas))]
    private int place;

    [ObservableProperty] private bool gameRunning = true;
    [ObservableProperty] private string gameStatus = "";
    [ObservableProperty] private string connectionStatus = "";

    public bool IsNow => Place == PlaceNow;
    public bool IsFights => Place == PlaceFights;
    public bool IsWorld => Place == PlaceWorld;
    public bool IsDaevas => Place == PlaceDaevas;

    [RelayCommand]
    private void Go(string? target)
    {
        if (int.TryParse(target, out var p) && p is >= PlaceNow and <= PlaceDaevas) Place = p;
    }

    public void Refresh()
    {
        GameRunning = Services.GameLauncher.IsRunning;
        GameStatus = Loc.T(GameRunning ? "shell.status.game_on" : "shell.status.game_off");
        // nie ein Icon ohne Wort: solange der Mitschnitt noch keinen Zustand meldet, „Suche Verbindung“
        ConnectionStatus = string.IsNullOrWhiteSpace(Overlay.StatusText) ? Loc.T("shell.status.searching") : Overlay.StatusText;
        if (IsNow) Now.Refresh();
    }
}
