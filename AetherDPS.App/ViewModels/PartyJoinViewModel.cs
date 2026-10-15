using System.Collections.ObjectModel;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using AetherDPS.Engine.Community;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>Karte eines neuen Gruppenmitglieds: erst die Werte aus dem Gruppen-Paket, dann das Profil von NCSOFT.</summary>
public sealed partial class PartyJoinCard : ObservableObject
{
    public PartyJoinCard(PartyMember member)
    {
        Name = member.Name;
        RawServer = member.ServerName;
        Subtitle = string.Join(" · ", new[] { EuServers.Display(member.ServerName), member.Level > 0 ? Loc.T("profile.level", member.Level) : "" }
            .Where(s => s.Length > 0));
        CombatPower = member.CombatPower > 0 ? member.CombatPower.ToString("N0", Loc.Culture) : "–";
        Status = Loc.T("armory.loading", member.Name);
    }

    public string Name { get; }
    public string RawServer { get; }
    [ObservableProperty] private string subtitle;
    [ObservableProperty] private string combatPower;
    [ObservableProperty] private string itemLevel = "–";
    [ObservableProperty] private string enchant = "–";
    [ObservableProperty] private string daevanion = "–";
    [ObservableProperty] private string weapon = "";
    [ObservableProperty] private IBrush weaponBrush = GradeColors.For("");
    [ObservableProperty] private string status;
    [ObservableProperty] private Bitmap? portrait;

    public void Apply(ArmoryProfile? p)
    {
        if (p is null)
        {
            Status = Loc.T("armory.not_found", Name);
            return;
        }
        Status = "";
        Subtitle = string.Join(" · ", new[] { p.ClassName, p.Server, Loc.T("profile.level", p.Level) }.Where(s => !string.IsNullOrEmpty(s)));
        CombatPower = p.CombatPower.ToString("N0", Loc.Culture);
        ItemLevel = p.ItemLevel.ToString("N0", Loc.Culture);
        Enchant = "+" + p.AverageEnchant.ToString("0.0", Loc.Culture);
        Daevanion = p.Boards.Count > 0 ? $"{p.DaevanionPercent} %" : "–";
        if (p.Equipment.FirstOrDefault(i => i.Slot == "MainHand") is { } w)
        {
            Weapon = (w.Enchant > 0 ? $"+{w.Enchant} " : "") + w.Name;
            WeaponBrush = GradeColors.For(w.Grade);
        }
        _ = RemoteImages.GetAsync(p.Portrait).ContinueWith(t =>
        {
            if (t.Result is { } bitmap) Dispatcher.UIThread.Post(() => Portrait = bitmap);
        }, TaskScheduler.Default);
    }
}

/// <summary>
/// Tritt jemand der Gruppe bei, erscheint seine Karte am Bildschirmrand (Profil von NCSOFT).
/// Abschaltbar im Reiter „Gruppe“. Der eigene Charakter wird nie eingeblendet.
/// </summary>
public sealed partial class PartyJoinViewModel : ObservableObject
{
    private const int MaxCards = 5;
    private readonly PartyRoster roster;
    private readonly Armory armory;
    private readonly MeterSettings settings;
    private readonly HashSet<string> known = new(StringComparer.Ordinal);

    public ObservableCollection<PartyJoinCard> Cards { get; } = [];

    /// <summary>Neue Karten sind da – Fenster zeigen.</summary>
    public event EventHandler? CardsAdded;

    /// <summary>Karte angeklickt – Profil im Hub öffnen.</summary>
    public event Action<string, string?>? ProfileRequested;

    /// <summary>Alle Karten weg – Fenster verstecken.</summary>
    public event EventHandler? Closed;

    public PartyJoinViewModel(PartyRoster roster, Armory armory, MeterSettings settings)
    {
        this.roster = roster;
        this.armory = armory;
        this.settings = settings;
    }

    public void Start() => roster.Updated += (_, _) => Dispatcher.UIThread.Post(OnRosterUpdated);

    internal void OnRosterUpdated()
    {
        var members = roster.Members;
        if (members.Count == 0)
        {
            known.Clear();   // Gruppe verlassen – beim nächsten Beitritt wieder alle zeigen
            return;
        }
        var current = members.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        known.IntersectWith(current);   // wer gegangen ist, darf beim Wiederkommen erneut erscheinen
        var fresh = members.Where(m => known.Add(m.Name) && !IsSelf(m.Name)).ToList();
        if (fresh.Count == 0 || !settings.PartyJoinPopup) return;

        foreach (var member in fresh.Take(MaxCards))
        {
            Cards.Insert(0, new PartyJoinCard(member));
            while (Cards.Count > MaxCards) Cards.RemoveAt(Cards.Count - 1);
            _ = LoadAsync(Cards[0], member);
        }
        CardsAdded?.Invoke(this, EventArgs.Empty);
    }

    private bool IsSelf(string name) => string.Equals(name, settings.CharacterName, StringComparison.Ordinal);

    private async Task LoadAsync(PartyJoinCard card, PartyMember member)
    {
        if (Armory.ServerId(member.ServerName) is not int serverId)
        {
            card.Status = Loc.T("armory.no_server");
            return;
        }
        var profile = await armory.LoadAsync(member.Name, serverId);
        Dispatcher.UIThread.Post(() => card.Apply(profile));
    }

    [RelayCommand]
    private void Open(PartyJoinCard card)
    {
        ProfileRequested?.Invoke(card.Name, card.RawServer);
        Close();
    }

    [RelayCommand]
    public void Close()
    {
        Cards.Clear();
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
