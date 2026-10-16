using System.Collections.ObjectModel;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

public sealed record PartyRow(string Name, string Server, string Level, string CombatPower, string ClassName,
    Bitmap? ClassIcon, IBrush ClassBrush, double PowerPercent, string RawServer);

public static class GradeColors
{
    private static readonly Dictionary<string, IBrush> Brushes = new()
    {
        // Wie im Spiel (EU-Client): Unique = Einzigartig gold, Legend blau
        ["Unique"] = new SolidColorBrush(Color.Parse("#E0A84A")),
        ["Legend"] = new SolidColorBrush(Color.Parse("#5AA0F0")),
        ["Epic"] = new SolidColorBrush(Color.Parse("#B07CF0")),
        ["Special"] = new SolidColorBrush(Color.Parse("#4CC3B0")),
        ["Rare"] = new SolidColorBrush(Color.Parse("#5CC26A")),
    };

    public static IBrush For(string grade) => Brushes.GetValueOrDefault(grade) ?? new SolidColorBrush(Color.Parse("#C9CED6"));
}

/// <summary>Reiter „Gruppe“: Mitglieder mit Stufe und Kampfkraft, auf Klick das Profil (Reiter „Profil“).</summary>
public sealed partial class PartyViewModel : ObservableObject
{
    private readonly PartyRoster roster;
    private readonly RunTracker tracker;
    private readonly MeterSettings settings;

    public ObservableCollection<PartyRow> Members { get; } = [];

    [ObservableProperty] private bool hasParty;
    [ObservableProperty] private string updated = "";

    /// <summary>Profil eines Mitglieds zeigen (Name, Server) – wechselt in den Reiter „Profil“.</summary>
    public event Action<string, string?>? ProfileRequested;

    public PartyViewModel(PartyRoster roster, RunTracker tracker, MeterSettings settings)
    {
        this.roster = roster;
        this.tracker = tracker;
        this.settings = settings;
        roster.Updated += (_, _) => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    /// <summary>Beim Gruppenbeitritt neue Mitglieder automatisch einblenden (Profil von NCSOFT).</summary>
    public bool JoinPopup
    {
        get => settings.PartyJoinPopup;
        set
        {
            if (settings.PartyJoinPopup == value) return;
            settings.PartyJoinPopup = value;
            settings.Save();
            OnPropertyChanged();
        }
    }

    public void Refresh()
    {
        var members = roster.Members;
        HasParty = members.Count > 0;
        Updated = roster.UpdatedAt is DateTime t ? Loc.T("party.updated", t) : "";

        // Klasse kennt das Gruppen-Paket nicht – aus den Kampfdaten nachschlagen, falls vorhanden
        var classes = tracker.Snapshot().Players
            .Where(p => p.IsIdentified)
            .GroupBy(p => p.Name)
            .ToDictionary(g => g.Key, g => g.First().ClassId);

        int maxPower = members.Count > 0 ? Math.Max(1, members.Max(m => m.CombatPower)) : 1;
        Members.Clear();
        foreach (var m in members.OrderByDescending(m => m.CombatPower))
        {
            int classId = classes.GetValueOrDefault(m.Name);
            Members.Add(new PartyRow(
                m.Name, EuServers.Display(m.ServerName), m.Level > 0 ? m.Level.ToString() : "–",
                m.CombatPower > 0 ? m.CombatPower.ToString("N0", Loc.Culture) : "–",
                classId > 0 ? GameText.ClassName(classId, "") : "",
                classId > 0 ? ClassVisuals.Icon(classId) : null,
                ClassVisuals.Brush(classId),
                m.CombatPower * 100.0 / maxPower,
                m.ServerName));
        }
    }

    [RelayCommand]
    private void ShowMember(PartyRow row) => ProfileRequested?.Invoke(row.Name, row.RawServer);
}
