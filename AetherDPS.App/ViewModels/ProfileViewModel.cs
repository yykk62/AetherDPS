using System.Collections.ObjectModel;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using AetherDPS.Engine.Community;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

public sealed record ServerOption(int Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Bild, das nachgeladen wird (Icon von NCSOFT).</summary>
public abstract partial class WithIcon : ObservableObject
{
    [ObservableProperty] private Bitmap? icon;

    protected void LoadIcon(string? url)
    {
        if (url is null) return;
        _ = RemoteImages.GetAsync(url).ContinueWith(t =>
        {
            if (t.Result is { } bitmap) Avalonia.Threading.Dispatcher.UIThread.Post(() => Icon = bitmap);
        }, TaskScheduler.Default);
    }
}

public sealed partial class GearRow : WithIcon
{
    public GearRow(ArmoryItem item)
    {
        Item = item;
        LoadIcon(item.Icon);
    }

    public ArmoryItem Item { get; }
    public string Name => (Item.Enchant > 0 ? $"+{Item.Enchant} " : "") + Item.Name;
    public string Slot => Loc.T("armory.slot." + Item.Slot) is var s && !s.StartsWith("armory.", StringComparison.Ordinal) ? s : Item.Slot;
    public IBrush NameBrush => GradeColors.For(Item.Grade);
    public string BaseName => Item.Name;
    public string EnchantText => Item.Enchant > 0 ? $"+{Item.Enchant}" : "";
    [ObservableProperty] private bool isSelected;
}

public sealed record StatRow(string Name, string Value, string Effects);

public sealed partial class BoardRow : WithIcon
{
    public BoardRow(ArmoryBoard board)
    {
        Name = board.Name;
        Text = $"{board.Open} / {board.Total}";
        Percent = board.Total > 0 ? board.Open * 100.0 / board.Total : 0;
        LoadIcon(board.Icon);
    }

    public string Name { get; }
    public string Text { get; }
    public double Percent { get; }
}

public sealed record TitleRow(string Category, string Name, IBrush Brush, string Text, double Percent);

public sealed partial class ProfileSkillRow : WithIcon
{
    public ProfileSkillRow(ArmorySkill skill)
    {
        Name = skill.Name;
        Level = skill.Level.ToString();
        LoadIcon(skill.Icon);
    }

    public string Name { get; }
    public string Level { get; }
}

public sealed record SkillGroup(string Title, IReadOnlyList<ProfileSkillRow> Skills);

/// <summary>
/// Reiter „Profil“: der eigene Charakter (oder ein beliebiger auf einem EU-Server) mit Ausrüstung,
/// Werten, Daevanion, Titeln und Skills – alles von NCSOFT, auch ohne laufendes Spiel.
/// </summary>
public sealed partial class ProfileViewModel : ObservableObject
{
    private readonly Armory armory;
    private readonly MeterSettings settings;
    private ArmoryProfile? profile;
    private bool selfLoaded;

    public ObservableCollection<GearRow> Armor { get; } = [];
    public ObservableCollection<GearRow> Jewelry { get; } = [];
    public ObservableCollection<StatRow> BaseStats { get; } = [];
    public ObservableCollection<StatRow> GodStats { get; } = [];
    public ObservableCollection<BoardRow> Boards { get; } = [];
    public ObservableCollection<TitleRow> Titles { get; } = [];
    public ObservableCollection<SkillGroup> SkillGroups { get; } = [];
    public ObservableCollection<StatRow> DetailStats { get; } = [];
    public IReadOnlyList<ServerOption> Servers { get; }

    [ObservableProperty] private string searchName = "";
    [ObservableProperty] private ServerOption? searchServer;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool hasProfile;
    /// <summary>Letzte Suche fehlgeschlagen (nicht gefunden, keine Verbindung): Statuszeile rot mit Warnsymbol.</summary>
    [ObservableProperty] private bool loadFailed;

    /// <summary>Leerer Zustand: kein Profil geladen und keine Suche unterwegs.</summary>
    public bool ShowEmpty => !HasProfile && !IsLoading;
    partial void OnHasProfileChanged(bool value) => OnPropertyChanged(nameof(ShowEmpty));
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowEmpty));

    [ObservableProperty] private Bitmap? portrait;
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string subtitle = "";
    [ObservableProperty] private string combatPower = "";
    [ObservableProperty] private string itemLevel = "";
    [ObservableProperty] private string averageEnchant = "";
    [ObservableProperty] private string daevanion = "";
    [ObservableProperty] private string titleCount = "";
    [ObservableProperty] private string extras = "";
    /// <summary>Profil des eigenen Charakters: Kampfkraft in Gold („du“).</summary>
    [ObservableProperty] private bool isSelf;

    [ObservableProperty] private bool hasDetail;
    /// <summary>„Details: 5 von 12 geladen“ – solange die Item-Details im Hintergrund nachgeladen werden.</summary>
    [ObservableProperty] private string prefetchText = "";
    [ObservableProperty] private bool detailLoading;
    [ObservableProperty] private string detailTitle = "";
    [ObservableProperty] private IBrush detailBrush = GradeColors.For("");
    [ObservableProperty] private string detailFacts = "";
    [ObservableProperty] private string detailGods = "";

    private readonly ProfileHistory history;

    /// <summary>Verlauf von Kampfkraft und Itemstufe (nur beim eigenen Charakter sichtbar).</summary>
    public SelfProgress Progress { get; }

    public ProfileViewModel(Armory armory, MeterSettings settings, ProfileHistory history, SelfProgress progress)
    {
        Progress = progress;
        this.armory = armory;
        this.history = history;
        this.settings = settings;
        Servers = GameText.Servers
            .Where(s => s.Key is >= 1300 and < 1400 or >= 2300 and < 2400)
            .Select(s => new ServerOption(s.Key, $"{s.Value} ({Loc.T(s.Key < 2000 ? "profile.elyos" : "profile.asmodian")})"))
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        ResetToSelf();
    }

    public bool KnowsSelf => !string.IsNullOrWhiteSpace(settings.CharacterName) && Armory.ServerId(settings.CharacterServer) is not null;

    /// <summary>Beim ersten Öffnen des Reiters den eigenen Charakter laden.</summary>
    public void EnsureLoaded()
    {
        if (selfLoaded || IsLoading) return;
        selfLoaded = true;
        if (KnowsSelf) _ = LoadAsync(settings.CharacterName!, Armory.ServerId(settings.CharacterServer)!.Value);
        else Status = Loc.T("profile.unknown_self");
    }

    /// <summary>Profil eines bestimmten Spielers zeigen (z. B. aus der Gruppe).</summary>
    public void Show(string characterName, string? server)
    {
        selfLoaded = true;
        if (Armory.ServerId(server) is not int id)
        {
            Status = Loc.T("armory.no_server");
            return;
        }
        SearchName = characterName;
        SearchServer = Servers.FirstOrDefault(s => s.Id == id);
        _ = LoadAsync(characterName, id);
    }

    [RelayCommand]
    private Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchName) || SearchServer is null)
        {
            Status = Loc.T("profile.need_input");
            return Task.CompletedTask;
        }
        return LoadAsync(SearchName.Trim(), SearchServer.Id);
    }

    [RelayCommand]
    private Task ShowSelf()
    {
        ResetToSelf();
        selfLoaded = true;
        return KnowsSelf ? LoadAsync(settings.CharacterName!, Armory.ServerId(settings.CharacterServer)!.Value) : Task.CompletedTask;
    }

    private void ResetToSelf()
    {
        SearchName = settings.CharacterName ?? "";
        var id = Armory.ServerId(settings.CharacterServer);
        SearchServer = Servers.FirstOrDefault(s => s.Id == id);
    }

    private async Task LoadAsync(string characterName, int serverId)
    {
        if (IsLoading) return;
        IsLoading = true;
        LoadFailed = false;
        Status = Loc.T("armory.loading", characterName);
        var loaded = await armory.LoadAsync(characterName, serverId);
        IsLoading = false;
        if (loaded is null)
        {
            LoadFailed = true;
            Status = Loc.T("armory.not_found", characterName);
            return;
        }
        Apply(loaded);
        Status = Loc.T("armory.source");
    }

    private void Apply(ArmoryProfile p)
    {
        profile = p;
        HasProfile = true;
        HasDetail = false;
        Name = p.Name;
        IsSelf = string.Equals(p.Name, settings.CharacterName, StringComparison.OrdinalIgnoreCase)
                 && Armory.ServerId(settings.CharacterServer) == p.ServerId;
        if (IsSelf) history.Add(p);   // eigenes Profil fließt in den Verlauf
        Subtitle = string.Join(" · ", new[] { p.ClassName, p.Race, p.Server, Loc.T("profile.level", p.Level) }.Where(s => !string.IsNullOrEmpty(s)));
        CombatPower = p.CombatPower.ToString("N0", Loc.Culture);
        ItemLevel = p.ItemLevel.ToString("N0", Loc.Culture);
        AverageEnchant = "+" + p.AverageEnchant.ToString("0.0", Loc.Culture);
        Daevanion = p.Boards.Count > 0 ? $"{p.DaevanionPercent} %" : "–";
        TitleCount = p.TitlesOwned.ToString(Loc.Culture);
        var extras = new List<string>();
        if (p.Pet is { } pet) extras.Add(Loc.T("profile.pet", pet.Name, pet.Level));
        if (p.Wing is { } wing) extras.Add(Loc.T("profile.wing", (wing.Level > 0 ? $"+{wing.Level} " : "") + wing.Name));
        Extras = string.Join("   ·   ", extras);
        Portrait = null;
        _ = RemoteImages.GetAsync(p.Portrait).ContinueWith(t =>
        {
            if (t.Result is { } bitmap && ReferenceEquals(profile, p)) Avalonia.Threading.Dispatcher.UIThread.Post(() => Portrait = bitmap);
        }, TaskScheduler.Default);

        Armor.Clear();
        Jewelry.Clear();
        foreach (var item in p.Equipment)
            (Armory.IsArmorSlot(item.Slot) ? Armor : Jewelry).Add(new GearRow(item));
        StartPrefetch(p);

        BaseStats.Clear();
        GodStats.Clear();
        foreach (var s in p.Stats)
        {
            var row = new StatRow(s.Name, s.Value.ToString(Loc.Culture), string.Join(" · ", s.Effects));
            (s.Name.Contains('[') ? GodStats : BaseStats).Add(row);
        }

        Boards.Clear();
        foreach (var b in p.Boards) Boards.Add(new BoardRow(b));

        Titles.Clear();
        foreach (var t in p.Titles)
            Titles.Add(new TitleRow(Loc.T("profile.titlecat." + t.Category) is var c && !c.StartsWith("profile.", StringComparison.Ordinal) ? c : t.Category,
                t.Name, GradeColors.For(t.Grade), $"{t.Owned} / {t.Total}", t.Total > 0 ? t.Owned * 100.0 / t.Total : 0));

        SkillGroups.Clear();
        foreach (var g in p.Skills.GroupBy(s => s.Category).OrderBy(g => g.Key switch { "Active" => 0, "Passive" => 1, _ => 2 }))
        {
            var title = Loc.T("profile.skillcat." + g.Key) is var t && !t.StartsWith("profile.", StringComparison.Ordinal) ? t : g.Key;
            SkillGroups.Add(new SkillGroup(title, g.Select(s => new ProfileSkillRow(s)).ToList()));
        }
    }

    private CancellationTokenSource? prefetch;
    private int userWaiting;

    /// <summary>
    /// Nach dem Profil die Details aller Teile im Hintergrund nachladen (eins nach dem anderen, höflich
    /// gedrosselt), damit ein Klick auf ein Teil sofort antwortet. Klickt der Nutzer, hat seine Anfrage Vorrang.
    /// </summary>
    private void StartPrefetch(ArmoryProfile p)
    {
        prefetch?.Cancel();
        var cts = prefetch = new CancellationTokenSource();
        var items = Armor.Concat(Jewelry).Select(r => r.Item).Where(i => i.Id != 0).ToList();
        if (items.Count == 0) { PrefetchText = ""; return; }
        _ = Task.Run(async () =>
        {
            int done = 0;
            foreach (var item in items)
            {
                if (cts.IsCancellationRequested) return;
                while (Volatile.Read(ref userWaiting) > 0) await Task.Delay(100, cts.Token);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(profile, p)) PrefetchText = Loc.T("profile.prefetch", done, items.Count);
                });
                try { await armory.LoadItemAsync(p, item, cts.Token); }
                catch (OperationCanceledException) { return; }
                done++;
            }
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(profile, p)) PrefetchText = "";
            });
        });
    }

    [RelayCommand]
    private async Task SelectItem(GearRow row)
    {
        if (profile is null) return;
        foreach (var r in Armor.Concat(Jewelry)) r.IsSelected = ReferenceEquals(r, row);
        HasDetail = true;
        DetailLoading = true;
        DetailTitle = row.Name;
        DetailBrush = row.NameBrush;
        DetailFacts = "";
        DetailGods = "";
        DetailStats.Clear();
        Interlocked.Increment(ref userWaiting);
        ArmoryItemDetail? detail;
        try { detail = await armory.LoadItemAsync(profile, row.Item); }
        finally { Interlocked.Decrement(ref userWaiting); }
        if (!row.IsSelected) return;   // inzwischen ein anderes Teil gewählt
        DetailLoading = false;
        if (detail is null)
        {
            DetailFacts = Loc.T("profile.item_failed");
            return;
        }
        DetailFacts = Loc.T("profile.item_facts", detail.Category, detail.Level, detail.Enchant, detail.MaxEnchant)
                      + (detail.Tradable ? " · " + Loc.T("profile.tradable") : "");
        foreach (var s in detail.MainStats) DetailStats.Add(new StatRow(s.Name, s.Value, ""));
        foreach (var s in detail.SubStats) DetailStats.Add(new StatRow(s.Name, s.Value, Loc.T("profile.substat")));
        foreach (var s in detail.MagicStones) DetailStats.Add(new StatRow(s.Name, s.Value, Loc.T("profile.manastone")));
        int empty = detail.MagicStoneSlots - detail.MagicStones.Count;
        if (empty > 0) DetailStats.Add(new StatRow(Loc.T("profile.empty_slots", empty), "", Loc.T("profile.manastone")));
        DetailGods = string.Join("\n\n", detail.GodStones.Select(g => $"{g.Name}\n{g.Description}"));
    }
}
