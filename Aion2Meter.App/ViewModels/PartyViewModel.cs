using System.Collections.ObjectModel;
using Aion2Meter.App.Services;
using Aion2Meter.Engine;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Meter.App.ViewModels;

public sealed record PartyRow(string Name, string Server, string Level, string CombatPower, string ClassName,
    Bitmap? ClassIcon, IBrush ClassBrush, double PowerPercent);

/// <summary>Reiter „Gruppe“: Mitglieder mit Stufe und Kampfkraft – nur für dich sichtbar.</summary>
public sealed partial class PartyViewModel : ObservableObject
{
    private readonly PartyRoster roster;
    private readonly RunTracker tracker;

    public ObservableCollection<PartyRow> Members { get; } = [];

    [ObservableProperty] private bool hasParty;
    [ObservableProperty] private string updated = "";

    public PartyViewModel(PartyRoster roster, RunTracker tracker)
    {
        this.roster = roster;
        this.tracker = tracker;
        roster.Updated += (_, _) => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    public void Refresh()
    {
        var members = roster.Members;
        HasParty = members.Count > 0;
        Updated = roster.UpdatedAt is DateTime t ? $"Stand {t:HH:mm:ss}" : "";

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
                m.Name, m.ServerName, m.Level > 0 ? m.Level.ToString() : "–",
                m.CombatPower > 0 ? m.CombatPower.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) : "–",
                classId > 0 ? GermanGameData.ClassName(classId, "") : "",
                classId > 0 ? ClassVisuals.Icon(classId) : null,
                ClassVisuals.Brush(classId),
                m.CombatPower * 100.0 / maxPower));
        }
    }
}
