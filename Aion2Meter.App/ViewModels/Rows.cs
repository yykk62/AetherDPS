using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aion2Meter.App.ViewModels;

public sealed partial class PlayerRow(long playerId) : ObservableObject
{
    public long PlayerId { get; } = playerId;

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string className = "";
    [ObservableProperty] private string dps = "";
    [ObservableProperty] private string total = "";
    [ObservableProperty] private string share = "";
    [ObservableProperty] private double barPercent;
    [ObservableProperty] private bool isUser;
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private IBrush classBrush = Brushes.Gray;
    [ObservableProperty] private long sortKey;
}

public sealed record SkillRow(string Name, string Damage, string Share, int Hits, string Crit, double BarPercent);

public static class ClassColors
{
    // Farben pro Klassen-ID (AION 2), bewusst gut unterscheidbar auf dunklem Grund.
    private static readonly Dictionary<int, string> Hex = new()
    {
        [11] = "#E0A84A", // Gladiator
        [12] = "#5B8DEF", // Templer
        [13] = "#B45CE0", // Assassine
        [14] = "#6CC36C", // Waldläufer
        [15] = "#E05C5C", // Magier
        [16] = "#4FC1C9", // Beschwörer
        [10] = "#4FC1C9", // Geist (Beschwörung)
        [17] = "#F2E27D", // Kleriker
        [18] = "#9BD07A", // Kantor
        [19] = "#E07F4F", // Faustkämpfer
    };

    private static readonly Dictionary<int, IBrush> Cache = [];

    public static IBrush For(int classId)
    {
        if (Cache.TryGetValue(classId, out var brush)) return brush;
        brush = new SolidColorBrush(Color.Parse(Hex.GetValueOrDefault(classId, "#8A8F98")));
        Cache[classId] = brush;
        return brush;
    }
}
