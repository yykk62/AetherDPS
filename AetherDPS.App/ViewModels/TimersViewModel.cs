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

    /// <summary>Als eigene, frei verschiebbare Leiste anzeigen.</summary>
    [RelayCommand]
    private void Detach() => owner.Service.SetDetached(Item.Name, true);

    /// <summary>Zurück in die Timer-Gruppe.</summary>
    [RelayCommand]
    private void Attach() => owner.Service.SetDetached(Item.Name, false);
}

/// <summary>
/// Bearbeitbarer Zeitplan – jede Änderung wird sofort gespeichert (Haken, Tage, Kategorie
/// direkt; Texte kurz nach dem Tippen, sobald sie gültig sind). Kein „Speichern“-Knopf nötig.
/// </summary>
public sealed partial class ScheduleEditor : ObservableObject
{
    private readonly TimersViewModel owner;
    private readonly Avalonia.Threading.DispatcherTimer saveTimer;
    private bool loading = true;

    public ScheduleEntry? Original { get; private set; }

    [ObservableProperty] private string name;
    [ObservableProperty] private string category;
    [ObservableProperty] private bool enabled;
    [ObservableProperty] private bool showInOverlay;
    [ObservableProperty] private bool reminder;
    [ObservableProperty] private string timesText;
    [ObservableProperty] private string intervalText;
    [ObservableProperty] private bool unconfirmed;
    [ObservableProperty] private bool mo;
    [ObservableProperty] private bool di;
    [ObservableProperty] private bool mi;
    [ObservableProperty] private bool don;
    [ObservableProperty] private bool fr;
    [ObservableProperty] private bool sa;
    [ObservableProperty] private bool so;
    [ObservableProperty] private string error = "";

    public IReadOnlyList<string> Categories { get; } = ["Riss", "Weltboss", "Event", "Sonstiges"];

    public ScheduleEditor(TimersViewModel owner, ScheduleEntry? original)
    {
        this.owner = owner;
        Original = original;
        var e = original ?? new ScheduleEntry { Name = Loc.T("timer.new_schedule"), Category = "Event", Times = ["20:00"] };
        name = e.Name;
        category = e.Category;
        enabled = e.Enabled;
        showInOverlay = e.ShowInOverlay;
        reminder = e.Reminder;
        timesText = string.Join(", ", e.Times);
        intervalText = e.IntervalHours > 0 ? e.IntervalHours.ToString("0.##", Loc.Culture) : "";
        unconfirmed = e.Unconfirmed;
        mo = e.Days.Contains(DayOfWeek.Monday);
        di = e.Days.Contains(DayOfWeek.Tuesday);
        mi = e.Days.Contains(DayOfWeek.Wednesday);
        don = e.Days.Contains(DayOfWeek.Thursday);
        fr = e.Days.Contains(DayOfWeek.Friday);
        sa = e.Days.Contains(DayOfWeek.Saturday);
        so = e.Days.Contains(DayOfWeek.Sunday);
        saveTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        saveTimer.Tick += (_, _) =>
        {
            saveTimer.Stop();
            Save();
        };
        loading = false;
        // Neuer Zeitplan: gleich anlegen, damit er sofort erscheint
        if (original is null) Save();
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (loading || e.PropertyName is nameof(Error) or nameof(Unconfirmed)) return;
        bool isText = e.PropertyName is nameof(Name) or nameof(TimesText) or nameof(IntervalText);
        saveTimer.Stop();
        if (isText) saveTimer.Start();   // beim Tippen kurz warten
        else Save();                     // Haken, Tage, Kategorie sofort
    }

    private void Save()
    {
        var times = TimesText.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => TimeSpan.TryParse(t, CultureInfo.InvariantCulture, out var ts) ? ts.ToString(@"hh\:mm") : null)
            .ToList();
        if (string.IsNullOrWhiteSpace(Name) || times.Count == 0 || times.Any(t => t is null))
        {
            Error = Loc.T("timer.error_name");
            return;
        }
        double interval = 0;
        if (!string.IsNullOrWhiteSpace(IntervalText) && !double.TryParse(IntervalText, Loc.Culture, out interval) && !double.TryParse(IntervalText, CultureInfo.InvariantCulture, out interval))
        {
            Error = Loc.T("timer.error_interval");
            return;
        }

        var days = new List<DayOfWeek>();
        if (Mo) days.Add(DayOfWeek.Monday);
        if (Di) days.Add(DayOfWeek.Tuesday);
        if (Mi) days.Add(DayOfWeek.Wednesday);
        if (Don) days.Add(DayOfWeek.Thursday);
        if (Fr) days.Add(DayOfWeek.Friday);
        if (Sa) days.Add(DayOfWeek.Saturday);
        if (So) days.Add(DayOfWeek.Sunday);

        var entry = new ScheduleEntry
        {
            Name = Name.Trim(), Category = Category, Enabled = Enabled, ShowInOverlay = ShowInOverlay, Reminder = Reminder,
            Times = times!, IntervalHours = interval, Days = days,
            // wer die Zeiten selbst angepasst hat, hat sie bestätigt
            Unconfirmed = Unconfirmed && Original is not null && times.SequenceEqual(Original.Times),
        };
        Error = "";
        Unconfirmed = entry.Unconfirmed;
        owner.SaveSchedule(Original, entry);
        Original = entry;
    }

    [RelayCommand]
    private void Delete()
    {
        saveTimer.Stop();
        owner.DeleteSchedule(this);
    }
}

/// <summary>Reiter „Timer“: alle Timer, Zeitplan-Editor, Feldbosse, eigene Timer.</summary>
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
    public ObservableCollection<ScheduleEditor> Schedules { get; } = [];

    [ObservableProperty] private string newName = "";
    [ObservableProperty] private string newMinutes = "";
    [ObservableProperty] private string reminderMinutes;
    [ObservableProperty] private bool overlayVisible;

    /// <summary>Timer-Overlay ein/aus (die App reagiert darauf).</summary>
    public event EventHandler? OverlayVisibilityChanged;

    public TimersViewModel(TimerService timers, FieldBosses fieldBosses)
    {
        this.timers = timers;
        this.fieldBosses = fieldBosses;
        BuildFieldBosses();
        reminderMinutes = timers.Settings.ReminderMinutes.ToString(CultureInfo.InvariantCulture);
        overlayVisible = timers.Settings.OverlayVisible;
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
        foreach (var s in timers.Schedules) Schedules.Add(new ScheduleEditor(this, s));
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

    // Eltnen und Morheim gibt es in der EU noch nicht – erscheinen erst, wenn der Server sie schickt
    private static readonly HashSet<int> EuMaps = [1010, 1110, 20, 22];

    private List<int> shownMaps = [];

    private List<int> VisibleMaps(Dictionary<int, FieldBossView> views) => fieldBosses.Maps
        .Where(map => EuMaps.Contains(map.Id) || HasData(map, views)).Select(m => m.Id).ToList();

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

    partial void OnOverlayVisibleChanged(bool value)
    {
        timers.Settings.OverlayVisible = value;
        timers.Save();
        OverlayVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

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
    private void AddSchedule() => Schedules.Insert(0, new ScheduleEditor(this, null));

    [RelayCommand]
    private void SaveReminder()
    {
        if (int.TryParse(ReminderMinutes, out var m) && m >= 0)
        {
            timers.Settings.ReminderMinutes = m;
            timers.Save();
        }
    }

    public void SaveSchedule(ScheduleEntry? original, ScheduleEntry updated)
    {
        // Editoren NICHT neu aufbauen – sonst verliert das Textfeld beim Tippen den Fokus
        if (original is null) timers.AddSchedule(updated);
        else timers.ReplaceSchedule(original, updated);
    }

    public void DeleteSchedule(ScheduleEditor editor)
    {
        if (editor.Original is not null) timers.RemoveSchedule(editor.Original);
        Schedules.Remove(editor);
    }

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
