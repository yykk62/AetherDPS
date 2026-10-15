using System.Collections.ObjectModel;
using System.Globalization;
using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

public static class TimerIcons
{
    public static string For(TimerItem item) => item.Kind switch
    {
        TimerKind.Boss or TimerKind.FieldBoss => "☠",
        TimerKind.Custom => "⏱",
        _ => item.Category switch { "Riss" => "◈", "Weltboss" => "♛", _ => "★" },
    };
}

public sealed partial class TimerRow(TimersViewModel owner, TimerItem item) : ObservableObject
{
    public TimerItem Item { get; } = item;
    public string Icon => TimerIcons.For(Item);
    public string Name => Item.Name + (Item.Unconfirmed ? " *" : "");
    public string Detail => Item.Detail ?? "";
    public bool IsBoss => Item.Kind == TimerKind.Boss;
    public bool IsCustom => Item.Kind == TimerKind.Custom;
    public bool IsSchedule => Item.Kind == TimerKind.Schedule;
    public bool NeedsRespawn => IsBoss && Item.DueAt is null;

    [ObservableProperty] private string remaining = "";
    [ObservableProperty] private string at = "";
    [ObservableProperty] private bool isDue;
    [ObservableProperty] private bool isSoon;
    [ObservableProperty] private string respawnMinutes = "";

    public void Update(DateTime now)
    {
        if (Item.DueAt is not DateTime due)
        {
            Remaining = Item.State ?? "–";
            At = "";
            return;
        }
        var left = due - now;
        IsDue = left <= TimeSpan.Zero;
        IsSoon = !IsDue && left <= TimeSpan.FromMinutes(10);
        At = due.Date == now.Date ? Loc.T("timer.at", due) : due.ToString("ddd HH:mm", Loc.Culture);
        Remaining = IsDue ? Loc.T("timer.now") : left.TotalHours >= 24 ? Loc.T("time.days_hours", (int)left.TotalDays, left.Hours)
            : left.TotalHours >= 1 ? left.ToString(@"h\:mm\:ss") : left.ToString(@"mm\:ss");
    }

    [RelayCommand]
    private void SaveRespawn()
    {
        if (int.TryParse(RespawnMinutes, out var minutes) && minutes > 0) owner.SetRespawn(Item.Name, minutes);
    }

    [RelayCommand]
    private void Ignore() => owner.Ignore(Item.Name);

    [RelayCommand]
    private void Start() => owner.StartCustom(Item.Name);

    [RelayCommand]
    private void Remove() => owner.Remove(Item.Kind, Item.Name);

    [RelayCommand]
    private void HideInOverlay() => owner.SetOverlay(Item, false);

}

/// <summary>Fester Termin (Riss, Belagerung …) – nur an/aus und Erinnerung, die Zeiten stimmen von selbst.</summary>
public sealed partial class ScheduleToggle : ObservableObject
{
    private readonly TimersViewModel owner;
    public ScheduleEntry Entry { get; }
    public string Name => Entry.DisplayName;
    public string Detail => Entry.Describe();

    [ObservableProperty] private bool enabled;
    [ObservableProperty] private bool reminder;

    public ScheduleToggle(TimersViewModel owner, ScheduleEntry entry)
    {
        this.owner = owner;
        Entry = entry;
        enabled = entry.Enabled;
        reminder = entry.Reminder;
    }

    partial void OnEnabledChanged(bool value) => owner.UpdateSchedule(Entry, value, Reminder);
    partial void OnReminderChanged(bool value) => owner.UpdateSchedule(Entry, Enabled, value);
}

/// <summary>Ein Feldboss im Reiter „Timer“: Stern = im Overlay und mit Erinnerung.</summary>
public sealed partial class FieldBossRow(FieldBosses service, FieldBossInfo boss) : ObservableObject
{
    public FieldBossInfo Boss { get; } = boss;
    public string Name => Boss.Name;

    [ObservableProperty] private bool isFavorite;
    [ObservableProperty] private string state = "";
    [ObservableProperty] private string at = "";
    [ObservableProperty] private bool isUp;

    public void Update(FieldBossView view, DateTime now)
    {
        IsFavorite = view.Favorite;
        IsUp = view.Status is FieldBossStatus.Alive or FieldBossStatus.ProbablyUp;
        At = view.Time is DateTime t && view.Status != FieldBossStatus.Unknown
            ? (t.Date == now.Date ? t.ToString("HH:mm", Loc.Culture) : t.ToString("ddd HH:mm", Loc.Culture)) : "";
        State = view.Status switch
        {
            FieldBossStatus.Alive => view.Time is DateTime since ? Loc.T("fb.alive_since", since) : Loc.T("fb.alive"),
            FieldBossStatus.Respawning or FieldBossStatus.Scheduled when view.Time is DateTime due =>
                Left(due - now) + (view.Estimated ? " ~" : ""),
            FieldBossStatus.ProbablyUp => Loc.T("fb.probably_up"),
            _ => Loc.T("fb.unknown"),
        };
    }

    private static string Left(TimeSpan left) =>
        left.TotalHours >= 24 ? Loc.T("time.days_hours", (int)left.TotalDays, left.Hours)
        : left.TotalHours >= 1 ? left.ToString(@"h\:mm\:ss") : left.ToString(@"mm\:ss");

    [RelayCommand]
    private void ToggleFavorite() => service.SetFavorite(Boss.Npc, !IsFavorite);
}

public sealed record OverlayModeOption(string Code, string Name)
{
    public override string ToString() => Name;
}

public sealed class FieldBossGroup(string name, bool expanded)
{
    public string Name { get; } = name;
    public bool Expanded { get; set; } = expanded;
    public ObservableCollection<FieldBossRow> Rows { get; } = [];
}

public sealed partial class TimersViewModel : ObservableObject
{
    private readonly TimerService timers;
    private readonly FieldBosses fieldBosses;

    /// <summary>Feldbosse je Karte (nur Karten, die es in der EU gibt oder für die schon Daten kamen).</summary>
    public ObservableCollection<FieldBossGroup> FieldBossGroups { get; } = [];

    [ObservableProperty] private bool fieldBossesKnown;

    public ObservableCollection<TimerRow> Rows { get; } = [];
    public ObservableCollection<ScheduleToggle> Schedules { get; } = [];

    [ObservableProperty] private string newName = "";
    [ObservableProperty] private string newMinutes = "";
    [ObservableProperty] private string reminderMinutes;

    /// <summary>Timer-Overlay ein/aus (die App reagiert darauf).</summary>
    public event EventHandler? OverlayVisibilityChanged;

    private readonly MeterSettings meterSettings;

    public IReadOnlyList<OverlayModeOption> OverlayModes { get; } =
    [
        new("always", Loc.T("timers.overlay_always")),
        new("soon", Loc.T("timers.overlay_soon")),
        new("off", Loc.T("timers.overlay_off")),
    ];

    [ObservableProperty] private OverlayModeOption selectedOverlayMode;
    [ObservableProperty] private bool isSoonMode;
    [ObservableProperty] private string soonMinutes;

    /// <summary>„always“, „soon“ oder „off“.</summary>
    public string OverlayMode => SelectedOverlayMode.Code;

    public int OverlaySoonMinutes => Math.Max(1, timers.Settings.OverlaySoonMinutes);

    public TimersViewModel(TimerService timers, FieldBosses fieldBosses, MeterSettings meterSettings)
    {
        this.timers = timers;
        this.fieldBosses = fieldBosses;
        this.meterSettings = meterSettings;
        var mode = timers.Settings.OverlayMode ?? (timers.Settings.OverlayVisible ? "always" : "off");
        selectedOverlayMode = OverlayModes.FirstOrDefault(m => m.Code == mode) ?? OverlayModes[0];
        isSoonMode = selectedOverlayMode.Code == "soon";
        soonMinutes = timers.Settings.OverlaySoonMinutes.ToString(CultureInfo.InvariantCulture);
        BuildFieldBosses();
        reminderMinutes = timers.Settings.ReminderMinutes.ToString(CultureInfo.InvariantCulture);
        timers.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);
        Rebuild();
        ReloadSchedules();
    }

    public TimerService Service => timers;

    private void Rebuild()
    {
        Rows.Clear();
        foreach (var item in timers.Items().OrderBy(i => i.DueAt ?? DateTime.MaxValue)) Rows.Add(new TimerRow(this, item));
        BuildFieldBosses();
        Tick();
    }

    public void ReloadSchedules()
    {
        Schedules.Clear();
        foreach (var s in timers.Schedules) Schedules.Add(new ScheduleToggle(this, s));
    }

    public void Tick()
    {
        var now = DateTime.Now;
        // Termin vorbei → nächsten Termin neu berechnen
        if (Rows.Any(r => r.Item.Kind == TimerKind.Schedule && r.Item.DueAt is DateTime due && due <= now))
        {
            Rebuild();
            return;
        }
        foreach (var row in Rows) row.Update(now);
        UpdateFieldBosses(now);
    }

    // Eltnen und Morheim gibt es in der EU noch nicht – erscheinen erst, wenn der Server sie schickt.
    // Verteron/Eltnen sind Elyos-Gebiete, Altgard/Morheim Asmodier-Gebiete, die Abyss teilen sich beide.
    private static readonly HashSet<int> EuMaps = [1010, 1110, 20, 22];
    private static readonly HashSet<int> ElyosMaps = [1010, 1011], AsmodianMaps = [1110, 1111];

    private List<int> shownMaps = [];

    /// <summary>Server 13xx = Elyos, 23xx = Asmodier (EU); unbekannt = beide.</summary>
    private HashSet<int>? OtherFactionMaps()
    {
        var server = meterSettings.CharacterServer;
        if (server is null || !server.StartsWith('#') || !int.TryParse(server.AsSpan(1), out var id)) return null;
        return (id / 1000) switch { 1 => AsmodianMaps, 2 => ElyosMaps, _ => null };
    }

    private List<int> VisibleMaps(Dictionary<int, FieldBossView> views)
    {
        var other = OtherFactionMaps();
        return fieldBosses.Maps
            .Where(map => (EuMaps.Contains(map.Id) && other?.Contains(map.Id) != true) || HasData(map, views))
            .Select(m => m.Id).ToList();
    }

    private static bool HasData(FieldBossMap map, Dictionary<int, FieldBossView> views) =>
        map.Bosses.Any(b => views.TryGetValue(b.Slot, out var v) && v.Status is not (FieldBossStatus.Unknown or FieldBossStatus.Scheduled));

    private void BuildFieldBosses()
    {
        var views = fieldBosses.Views().ToDictionary(v => v.Boss.Slot);
        var visible = VisibleMaps(views);
        if (visible.SequenceEqual(shownMaps) && FieldBossGroups.Count > 0) return;
        shownMaps = visible;
        FieldBossGroups.Clear();
        foreach (var map in fieldBosses.Maps.Where(m => visible.Contains(m.Id)))
        {
            bool hasData = HasData(map, views);
            var group = new FieldBossGroup(map.Name, hasData || map.Bosses.Any(b => views.TryGetValue(b.Slot, out var v) && v.Favorite));
            foreach (var boss in map.Bosses) group.Rows.Add(new FieldBossRow(fieldBosses, boss));
            FieldBossGroups.Add(group);
        }
        UpdateFieldBosses(DateTime.Now);
    }

    private void UpdateFieldBosses(DateTime now)
    {
        var views = fieldBosses.Views(now).ToDictionary(v => v.Boss.Slot);
        foreach (var row in FieldBossGroups.SelectMany(g => g.Rows))
            if (views.TryGetValue(row.Boss.Slot, out var view)) row.Update(view, now);
        FieldBossesKnown = fieldBosses.HasData;
    }

    partial void OnSelectedOverlayModeChanged(OverlayModeOption value)
    {
        if (value is null) return;
        IsSoonMode = value.Code == "soon";
        timers.Settings.OverlayMode = value.Code;
        timers.Settings.OverlayVisible = value.Code != "off";
        timers.Save();
        OverlayVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSoonMinutesChanged(string value)
    {
        if (!int.TryParse(value, out var m) || m is < 1 or > 180 || m == timers.Settings.OverlaySoonMinutes) return;
        timers.Settings.OverlaySoonMinutes = m;
        timers.Save();
        OverlayVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Pills gesperrt (Klicks gehen ins Spiel) – entsperren hier oder im Tray.</summary>
    public bool OverlayLocked
    {
        get => timers.Settings.OverlayLocked;
        set
        {
            if (timers.Settings.OverlayLocked == value) return;
            timers.Settings.OverlayLocked = value;
            timers.SaveQuiet();
            OnPropertyChanged();
        }
    }

    /// <summary>✕ im Overlay: aus.</summary>
    public void HideOverlay() => SelectedOverlayMode = OverlayModes.First(m => m.Code == "off");

    [RelayCommand]
    private void AddCustom()
    {
        if (int.TryParse(NewMinutes, out var minutes) && minutes > 0 && !string.IsNullOrWhiteSpace(NewName))
        {
            timers.AddCustom(NewName, minutes);
            NewName = "";
            NewMinutes = "";
        }
    }

    [RelayCommand]
    private void SaveReminder()
    {
        if (int.TryParse(ReminderMinutes, out var m) && m >= 0)
        {
            timers.Settings.ReminderMinutes = m;
            timers.Save();
        }
    }

    public void UpdateSchedule(ScheduleEntry entry, bool enabled, bool reminder) => timers.SetSchedule(entry, enabled, reminder);

    public void SetRespawn(string boss, int minutes) => timers.SetBossRespawn(boss, minutes);
    public void Ignore(string boss) => timers.IgnoreBoss(boss);
    public void StartCustom(string name) => timers.StartCustom(name);

    public void Remove(TimerKind kind, string name)
    {
        timers.Remove(kind, name);
        if (kind == TimerKind.Schedule) ReloadSchedules();
    }

    public void SetOverlay(TimerItem item, bool show)
    {
        timers.SetOverlayFlag(item.Kind, item.Name, show);
        ReloadSchedules();
    }
}
