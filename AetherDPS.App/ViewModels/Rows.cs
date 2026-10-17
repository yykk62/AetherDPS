using AetherDPS.App.Services;
using AetherDPS.Engine;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AetherDPS.App.ViewModels;

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

/// <summary>Spielerzeilen eines gespeicherten Laufs, nach Schaden sortiert (Balken relativ zum Besten).</summary>
public static class PlayerRows
{
    public static IEnumerable<PlayerRow> From(ArchivedRun run)
    {
        var ranked = run.Players.OrderByDescending(p => p.Damage).ToList();
        long top = ranked.Count > 0 ? ranked[0].Damage : 0;
        return ranked.Select(p => new PlayerRow(p.PlayerId)
        {
            Name = p.Name.StartsWith("Player_", StringComparison.Ordinal) ? Loc.T("player.unknown", p.PlayerId) : p.Name,
            ClassName = GameText.ClassName(p.ClassId, ""),
            ClassBrush = ClassVisuals.Brush(p.ClassId),
            ClassIcon = ClassVisuals.Icon(p.ClassId),
            Dps = Format.CompactFixed(p.Dps) + "/s",
            Total = Format.CompactFixed(p.Damage),
            Share = Format.Percent(p.Share),
            BarPercent = top > 0 ? p.Damage * 100.0 / top : 0,
            IsUser = p.IsUser,
        });
    }
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
        [13] = ("#B3CF3F", "assassin"),
        [14] = ("#2BA88F", "ranger"),
        [15] = ("#A15CE0", "sorcerer"),
        [16] = ("#D65CC8", "elementalist"),
        [17] = ("#EFE3B0", "cleric"),   // helles Elfenbein statt Gold (Gold heißt „du“)
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
                using var stream = AssetLoader.Open(new Uri($"avares://AetherDPS/Assets/Classes/{v.Icon}.png"));
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
