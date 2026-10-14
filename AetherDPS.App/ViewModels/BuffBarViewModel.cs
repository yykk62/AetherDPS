using System.Collections.ObjectModel;
using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AetherDPS.App.ViewModels;

public sealed partial class BuffBarRow : ObservableObject
{
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string remaining = "";
    [ObservableProperty] private double fraction;
}

public sealed partial class BuffBarViewModel(LiveTimers live) : ObservableObject
{
    private const int MaxRows = 10;

    public ObservableCollection<BuffBarRow> Items { get; } = [];

    [ObservableProperty] private bool hasItems;

    public void Refresh()
    {
        var active = live.Active().Take(MaxRows).ToList();
        while (Items.Count < active.Count) Items.Add(new BuffBarRow());
        while (Items.Count > active.Count) Items.RemoveAt(Items.Count - 1);
        for (int i = 0; i < active.Count; i++)
        {
            var a = active[i];
            var row = Items[i];
            row.Name = a.Name;
            row.Remaining = a.Remaining.TotalSeconds >= 60 ? a.Remaining.ToString(@"m\:ss") : $"{a.Remaining.TotalSeconds:0} s";
            row.Fraction = a.Fraction;
        }
        HasItems = active.Count > 0;
    }
}
