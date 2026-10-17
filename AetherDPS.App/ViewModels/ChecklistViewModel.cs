using System.Collections.ObjectModel;
using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

public sealed partial class ChecklistRow(ChecklistViewModel owner, ChecklistItem item, bool done) : ObservableObject
{
    public ChecklistItem Item { get; } = item;
    public string Text => Item.Text;

    [ObservableProperty] private bool isDone = done;

    partial void OnIsDoneChanged(bool value) => owner.SetDone(this, value);

    [RelayCommand]
    private void Remove() => owner.Remove(this);
}

/// <summary>Reiter „Checkliste“: tägliche und wöchentliche Aufgaben mit automatischem Reset.</summary>
public sealed partial class ChecklistViewModel : ObservableObject
{
    private readonly ChecklistService service;
    private bool loading;

    public ObservableCollection<ChecklistRow> Daily { get; } = [];
    public ObservableCollection<ChecklistRow> Weekly { get; } = [];
    public IReadOnlyList<string> Days => Loc.Culture.DateTimeFormat.DayNames;
    public IReadOnlyList<string> Hours { get; } = Enumerable.Range(0, 24).Select(h => $"{h:00}:00").ToList();

    [ObservableProperty] private string newText = "";
    [ObservableProperty] private bool newIsWeekly;
    [ObservableProperty] private string dailyInfo = "";
    [ObservableProperty] private string weeklyInfo = "";
    [ObservableProperty] private string dailyProgress = "";
    [ObservableProperty] private string weeklyProgress = "";
    [ObservableProperty] private double dailyPercent;
    [ObservableProperty] private double weeklyPercent;
    [ObservableProperty] private int dailyHour;
    [ObservableProperty] private int weeklyDay;
    [ObservableProperty] private int weeklyHour;

    public ChecklistViewModel(ChecklistService service)
    {
        this.service = service;
        dailyHour = service.Data.DailyResetHour;
        weeklyDay = (int)service.Data.WeeklyResetDay;
        weeklyHour = service.Data.WeeklyResetHour;
        Refresh();
    }

    public void Refresh()
    {
        loading = true;
        var now = DateTime.Now;
        Fill(Daily, ChecklistPeriod.Daily, now);
        Fill(Weekly, ChecklistPeriod.Weekly, now);
        DailyInfo = Info(ChecklistPeriod.Daily, now);
        WeeklyInfo = Info(ChecklistPeriod.Weekly, now);
        DailyProgress = Loc.T("now.next_done", Daily.Count(r => r.IsDone), Daily.Count);
        WeeklyProgress = Loc.T("now.next_done", Weekly.Count(r => r.IsDone), Weekly.Count);
        DailyPercent = Daily.Count == 0 ? 0 : Daily.Count(r => r.IsDone) * 100.0 / Daily.Count;
        WeeklyPercent = Weekly.Count == 0 ? 0 : Weekly.Count(r => r.IsDone) * 100.0 / Weekly.Count;
        loading = false;
    }

    private void Fill(ObservableCollection<ChecklistRow> target, ChecklistPeriod period, DateTime now)
    {
        var items = service.Data.Items.Where(i => i.Period == period).ToList();
        var states = items.Select(i => service.IsDone(i, now)).ToList();
        if (target.Select(r => r.Item).SequenceEqual(items) && target.Select(r => r.IsDone).SequenceEqual(states)) return;
        target.Clear();
        for (int i = 0; i < items.Count; i++) target.Add(new ChecklistRow(this, items[i], states[i]));
    }

    /// <summary>„Neu in 3 Std. 42 Min. (Mi 06:00 Uhr)“</summary>
    private string Info(ChecklistPeriod period, DateTime now)
    {
        var next = service.NextReset(period, now);
        var left = next - now;
        var leftText = left.TotalHours >= 24 ? Loc.T("time.days_hours", (int)left.TotalDays, left.Hours) : Loc.T("time.hours_minutes", (int)left.TotalHours, left.Minutes);
        return Loc.T("check.reset_in", leftText, next.ToString("ddd HH:mm", Loc.Culture));
    }

    public void SetDone(ChecklistRow row, bool done)
    {
        if (loading) return;
        service.SetDone(row.Item, done);
        Refresh();
    }

    public void Remove(ChecklistRow row)
    {
        service.Remove(row.Item);
        Refresh();
    }

    [RelayCommand]
    private void Add()
    {
        service.Add(NewText, NewIsWeekly ? ChecklistPeriod.Weekly : ChecklistPeriod.Daily);
        NewText = "";
        Refresh();
    }

    partial void OnDailyHourChanged(int value) => SaveResets();
    partial void OnWeeklyDayChanged(int value) => SaveResets();
    partial void OnWeeklyHourChanged(int value) => SaveResets();

    private void SaveResets()
    {
        service.Data.DailyResetHour = Math.Clamp(DailyHour, 0, 23);
        service.Data.WeeklyResetDay = (DayOfWeek)Math.Clamp(WeeklyDay, 0, 6);
        service.Data.WeeklyResetHour = Math.Clamp(WeeklyHour, 0, 23);
        service.Save();
        Refresh();
    }
}
