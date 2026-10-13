using System.Collections.ObjectModel;
using Aion2Meter.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Meter.App.ViewModels;

public sealed partial class TimerRow(TimersViewModel owner, TimerItem item) : ObservableObject
{
    public TimerItem Item { get; } = item;
    public string Icon => Item.Kind switch { TimerKind.Rift => "◈", TimerKind.Boss => "☠", _ => "⏱" };
    public string Name => Item.Name;
    public string Detail => Item.Detail ?? (Item.DueAt is DateTime d ? $"um {d:HH:mm}" : "");
    public bool IsBoss => Item.Kind == TimerKind.Boss;
    public bool IsCustom => Item.Kind == TimerKind.Custom;
    public bool NeedsRespawn => IsBoss && Item.DueAt is null;

    [ObservableProperty] private string remaining = "";
    [ObservableProperty] private bool isDue;
    [ObservableProperty] private string respawnMinutes = "";

    public void Update(DateTime now)
    {
        if (Item.DueAt is not DateTime due)
        {
            Remaining = "–";
            return;
        }
        var left = due - now;
        IsDue = left <= TimeSpan.Zero;
        Remaining = IsDue ? "jetzt" : left.TotalHours >= 1 ? left.ToString(@"h\:mm\:ss") : left.ToString(@"mm\:ss");
    }

    [RelayCommand]
    private void SaveRespawn()
    {
        if (int.TryParse(RespawnMinutes, out var minutes) && minutes > 0) owner.SetRespawn(Name, minutes);
    }

    [RelayCommand]
    private void Ignore() => owner.Ignore(Name);

    [RelayCommand]
    private void Start() => owner.StartCustom(Name);

    [RelayCommand]
    private void Remove() => owner.Remove(Item.Kind, Name);
}

/// <summary>Reiter „Timer“: Risse, Feldbosse (automatisch), eigene Timer.</summary>
public sealed partial class TimersViewModel : ObservableObject
{
    private readonly TimerService timers;

    public ObservableCollection<TimerRow> Rows { get; } = [];

    [ObservableProperty] private string newName = "";
    [ObservableProperty] private string newMinutes = "";
    [ObservableProperty] private string riftAnchor;
    [ObservableProperty] private string riftInterval;
    [ObservableProperty] private string reminderMinutes;
    [ObservableProperty] private bool riftReminder;

    public TimersViewModel(TimerService timers)
    {
        this.timers = timers;
        riftAnchor = timers.Settings.RiftAnchor;
        riftInterval = timers.Settings.RiftIntervalHours.ToString(System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        reminderMinutes = timers.Settings.ReminderMinutes.ToString();
        riftReminder = timers.Settings.RiftReminder;
        timers.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);
        Rebuild();
    }

    private void Rebuild()
    {
        Rows.Clear();
        foreach (var item in timers.Items()) Rows.Add(new TimerRow(this, item));
        Tick();
    }

    public void Tick()
    {
        var now = DateTime.Now;
        // Riss-Zeit rollt weiter → neu aufbauen, wenn der angezeigte Riss vorbei ist
        if (Rows.FirstOrDefault(r => r.Item.Kind == TimerKind.Rift) is { Item.DueAt: DateTime due } && due <= now)
        {
            Rebuild();
            return;
        }
        foreach (var row in Rows) row.Update(now);
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
    private void SaveSettings()
    {
        var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        if (TimeSpan.TryParse(RiftAnchor, out _)) timers.Settings.RiftAnchor = RiftAnchor;
        if (double.TryParse(RiftInterval, de, out var h) && h > 0) timers.Settings.RiftIntervalHours = h;
        if (int.TryParse(ReminderMinutes, out var m) && m >= 0) timers.Settings.ReminderMinutes = m;
        timers.Settings.RiftReminder = RiftReminder;
        timers.Save();
    }

    public void SetRespawn(string boss, int minutes) => timers.SetBossRespawn(boss, minutes);
    public void Ignore(string boss) => timers.IgnoreBoss(boss);
    public void StartCustom(string name) => timers.StartCustom(name);
    public void Remove(TimerKind kind, string name) => timers.Remove(kind, name);
}
