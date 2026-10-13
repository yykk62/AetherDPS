using System.Collections.ObjectModel;
using Aion2Meter.Engine;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Meter.App.ViewModels;

public sealed partial class BuffBarRow : ObservableObject
{
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string remaining = "";
    [ObservableProperty] private double fraction;
    [ObservableProperty] private IBrush brush = Brushes.Gray;
}

public sealed partial class BuffBarViewModel(LiveTimers live) : ObservableObject
{
    private static readonly IBrush BuffBrush = new SolidColorBrush(Color.Parse("#E0A84A"));
    private static readonly IBrush CooldownBrush = new SolidColorBrush(Color.Parse("#5B8DEF"));
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
            row.Name = a.Kind == LiveKind.Cooldown ? $"⟳ {a.Name}" : a.Name;
            row.Remaining = a.Remaining.TotalSeconds >= 60 ? a.Remaining.ToString(@"m\:ss") : $"{a.Remaining.TotalSeconds:0} s";
            row.Fraction = a.Fraction;
            row.Brush = a.Kind == LiveKind.Buff ? BuffBrush : CooldownBrush;
        }
        HasItems = active.Count > 0;
    }
}
