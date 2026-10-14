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

public sealed record PartyRow(string Name, string Server, string Level, string CombatPower, string ClassName,
    Bitmap? ClassIcon, IBrush ClassBrush, double PowerPercent, string RawServer);

/// <summary>Ausrüstungsteil in der Profil-Ansicht (Icon wird nachgeladen).</summary>
public sealed partial class ArmoryItemRow(ArmoryItem item) : ObservableObject
{
    public string Name => (item.Enchant > 0 ? $"+{item.Enchant} " : "") + item.Name;
    public string Slot => Loc.T("armory.slot." + item.Slot) is var s && !s.StartsWith("armory.", StringComparison.Ordinal) ? s : item.Slot;
    public IBrush GradeBrush { get; } = GradeColors.For(item.Grade);
    public string? IconUrl => item.Icon;
    [ObservableProperty] private Bitmap? icon;
}

public static class GradeColors
{
    private static readonly Dictionary<string, IBrush> Brushes = new()
    {
        ["Unique"] = new SolidColorBrush(Color.Parse("#E0A84A")),
        ["Legend"] = new SolidColorBrush(Color.Parse("#F07C4A")),
        ["Epic"] = new SolidColorBrush(Color.Parse("#B07CF0")),
        ["Special"] = new SolidColorBrush(Color.Parse("#4CC3B0")),
        ["Rare"] = new SolidColorBrush(Color.Parse("#5AA0F0")),
    };

    public static IBrush For(string grade) => Brushes.GetValueOrDefault(grade) ?? new SolidColorBrush(Color.Parse("#C9CED6"));
}

/// <summary>Reiter „Gruppe“: Mitglieder mit Stufe und Kampfkraft, auf Klick das Profil von NCSOFT.</summary>
public sealed partial class PartyViewModel : ObservableObject
{
    private static readonly HttpClient IconHttp = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly Dictionary<string, Bitmap> IconCache = [];

    private readonly PartyRoster roster;
    private readonly RunTracker tracker;
    private readonly Armory armory;
    private readonly MeterSettings settings;

    public ObservableCollection<PartyRow> Members { get; } = [];
    public ObservableCollection<ArmoryItemRow> Equipment { get; } = [];

    [ObservableProperty] private bool hasParty;
    [ObservableProperty] private string updated = "";

    [ObservableProperty] private bool hasProfile;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string profileStatus = "";
    [ObservableProperty] private string profileTitle = "";
    [ObservableProperty] private string profileFacts = "";
    [ObservableProperty] private string profileSkills = "";

    public PartyViewModel(PartyRoster roster, RunTracker tracker, Armory armory, MeterSettings settings)
    {
        this.roster = roster;
        this.tracker = tracker;
        this.armory = armory;
        this.settings = settings;
        roster.Updated += (_, _) => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    public bool CanShowSelf => !string.IsNullOrWhiteSpace(settings.CharacterName) && Armory.ServerId(settings.CharacterServer) is not null;

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
        OnPropertyChanged(nameof(CanShowSelf));
    }

    [RelayCommand]
    private Task ShowMemberAsync(PartyRow row) => LoadAsync(row.Name, row.RawServer);

    [RelayCommand]
    private Task ShowSelfAsync() => LoadAsync(settings.CharacterName ?? "", settings.CharacterServer);

    private async Task LoadAsync(string name, string? server)
    {
        if (IsLoading) return;
        if (Armory.ServerId(server) is not int serverId)
        {
            ProfileStatus = Loc.T("armory.no_server");
            return;
        }
        IsLoading = true;
        ProfileStatus = Loc.T("armory.loading", name);
        var profile = await armory.LoadAsync(name, serverId);
        IsLoading = false;
        if (profile is null)
        {
            ProfileStatus = Loc.T("armory.not_found", name);
            return;
        }
        ProfileStatus = Loc.T("armory.source");
        HasProfile = true;
        ProfileTitle = $"{profile.Name} · {profile.ClassName} · {profile.Server}";
        ProfileFacts = Loc.T("armory.facts", profile.Level, profile.ItemLevel.ToString("N0", Loc.Culture), profile.CombatPower.ToString("N0", Loc.Culture));
        ProfileSkills = string.Join(" · ", profile.Skills.Where(s => s.Category != "Passive").Take(14).Select(s => $"{s.Name} {s.Level}"));
        Equipment.Clear();
        foreach (var item in profile.Equipment) Equipment.Add(new ArmoryItemRow(item));
        _ = LoadIconsAsync(Equipment.ToList());
    }

    private static async Task LoadIconsAsync(List<ArmoryItemRow> rows)
    {
        foreach (var row in rows)
        {
            // nur Bilder vom offiziellen NCSOFT-Server
            if (row.IconUrl is not { } url || !url.StartsWith("https://assets.playnccdn.com/", StringComparison.Ordinal)) continue;
            Bitmap? bitmap;
            lock (IconCache) IconCache.TryGetValue(url, out bitmap);
            if (bitmap is null)
            {
                try
                {
                    var bytes = await IconHttp.GetByteArrayAsync(url);
                    bitmap = new Bitmap(new MemoryStream(bytes));
                    lock (IconCache) IconCache[url] = bitmap;
                }
                catch (Exception)
                {
                    continue;
                }
            }
            row.Icon = bitmap;
        }
    }
}
