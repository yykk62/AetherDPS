using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
    [ObservableProperty] private Bitmap? classIcon;
    [ObservableProperty] private long sortKey;
    [ObservableProperty] private string combatPower = "";
}

public sealed record SkillRow(string Name, string Damage, string Share, int Hits, string Crit, double BarPercent);

/// <summary>Farbe und Icon pro Klassen-ID (AION 2). Farben passend zu den Klassen-Icons.</summary>
public static class ClassVisuals
{
    private static readonly Dictionary<int, (string Color, string Icon)> ByClass = new()
    {
        [10] = ("#D65CC8", "elementalist"), // Geist (Beschwörung)
        [11] = ("#4FC1C9", "gladiator"),
        [12] = ("#5B8DEF", "templar"),
        [13] = ("#8BC34A", "assassin"),
        [14] = ("#3FAE7A", "ranger"),
        [15] = ("#A15CE0", "sorcerer"),
        [16] = ("#D65CC8", "elementalist"),
        [17] = ("#E0B84A", "cleric"),
        [18] = ("#E08A3C", "chanter"),
        [19] = ("#D64545", "brawler"),
    };

    private static readonly Dictionary<int, IBrush> Brushes = [];
    private static readonly Dictionary<int, Bitmap?> Icons = [];

    public static IBrush Brush(int classId)
    {
        if (Brushes.TryGetValue(classId, out var brush)) return brush;
        var hex = ByClass.TryGetValue(classId, out var v) ? v.Color : "#8A8F98";
        return Brushes[classId] = new SolidColorBrush(Color.Parse(hex));
    }

    public static Bitmap? Icon(int classId)
    {
        if (Icons.TryGetValue(classId, out var icon)) return icon;
        icon = null;
        if (ByClass.TryGetValue(classId, out var v))
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri($"avares://Aion2Meter/Assets/Classes/{v.Icon}.png"));
                icon = new Bitmap(stream);
            }
            catch (Exception)
            {
                // Fehlendes Icon ist kein Grund abzustürzen – dann eben ohne.
            }
        }
        return Icons[classId] = icon;
    }
}
