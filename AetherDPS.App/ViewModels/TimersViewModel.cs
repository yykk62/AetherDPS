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
        TimerKind.Boss => "☠",
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
            Remaining = "–";
            At = "";
            return;
        }
        var left = due - now;
        IsDue = left <= TimeSpan.Zero;
        IsSoon = !IsDue && left <= TimeSpan.FromMinutes(10);
        At = due.Date == now.Date ? $"um {due:HH:mm}" : $"{due:ddd HH:mm}";
        Remaining = IsDue ? "jetzt" : left.TotalHours >= 24 ? $"{(int)left.TotalDays} T {left.Hours} Std."
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
public sealed partial class TimersViewModel : ObservableObject
{
    private readonly TimerService timers;

    public ObservableCollection<TimerRow> Rows { get; } = [];
    public ObservableCollection<ScheduleEditor> Schedules { get; } = [];

    [ObservableProperty] private string newName = "";
    [ObservableProperty] private string newMinutes = "";
    [ObservableProperty] private string reminderMinutes;
    [ObservableProperty] private bool overlayVisible;

    /// <summary>Timer-Overlay ein/aus (die App reagiert darauf).</summary>
    public event EventHandler? OverlayVisibilityChanged;

    public TimersViewModel(TimerService timers)
    {
        this.timers = timers;
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
