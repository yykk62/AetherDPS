using System.Collections.ObjectModel;
using AetherDPS.App.Services;
using AetherDPS.Engine;
using AetherDPS.Engine.Community;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AetherDPS.App.ViewModels;

/// <summary>Ein Punkt einer Verlaufskurve.</summary>
public sealed record TrendPoint(DateTime At, double Value);

/// <summary>Kleines Ausrüstungsbild für die Karte „Du“ (Rahmen in Seltenheitsfarbe).</summary>
public sealed partial class GearIcon : WithIcon
{
    public GearIcon(ProfileGear gear)
    {
        Brush = GradeColors.For(gear.Grade);
        Enchant = gear.Enchant > 0 ? $"+{gear.Enchant}" : "";
        LoadIcon(gear.Icon);
    }

    public IBrush Brush { get; }
    public string Enchant { get; }
}

/// <summary>
/// Dein Charakter über die Zeit (docs/DESIGN.md, „Jetzt“ und Daevas › Profil): letzter Stand mit Porträt,
/// Ausrüstung, Kampfkraft und Gear Score, dazu die Veränderung seit einer Woche und die Verlaufskurven.
/// Daten aus <see cref="ProfileHistory"/> (lokal); abgerufen beim Öffnen des eigenen Profils und alle 4 Std.
/// </summary>
public sealed partial class SelfProgress : ObservableObject
{
    private readonly ProfileHistory history;
    private readonly MeterSettings settings;

    public ObservableCollection<GearIcon> Gear { get; } = [];

    [ObservableProperty] private bool hasData;
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string subtitle = "";
    [ObservableProperty] private Bitmap? portrait;
    [ObservableProperty] private string combatPower = "";
    [ObservableProperty] private string combatPowerDelta = "";
    [ObservableProperty] private bool combatPowerUp;
    [ObservableProperty] private string itemLevel = "";
    [ObservableProperty] private string itemLevelDelta = "";
    [ObservableProperty] private bool itemLevelUp;
    [ObservableProperty] private IReadOnlyList<TrendPoint> combatPowerPoints = [];
    [ObservableProperty] private IReadOnlyList<TrendPoint> itemLevelPoints = [];
    [ObservableProperty] private string caption = "";
    [ObservableProperty] private bool hasTrend;

    public SelfProgress(ProfileHistory history, MeterSettings settings)
    {
        this.history = history;
        this.settings = settings;
        history.Changed += (_, _) => Dispatcher.UIThread.Post(Refresh);
        settings.Changed += (_, _) => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    private string? shownPortrait;

    public void Refresh()
    {
        var server = Armory.ServerId(settings.CharacterServer);
        var latest = string.IsNullOrWhiteSpace(settings.CharacterName) || server is null ? null : history.Latest(settings.CharacterName!, server.Value);
        HasData = latest is not null;
        if (latest is null) return;

        // Kurve und Veränderung über denselben Zeitraum: die letzten 30 Tage
        var points = history.Points(latest.Name, server!.Value).Where(p => p.At >= latest.At.AddDays(-30)).ToList();
        Name = latest.Name;
        Subtitle = string.Join(" · ", new[] { latest.ClassName, latest.Server }.Where(s => !string.IsNullOrEmpty(s)));
        CombatPower = latest.CombatPower.ToString("N0", Loc.Culture);
        ItemLevel = latest.ItemLevel.ToString("N0", Loc.Culture);
        CombatPowerPoints = points.Select(p => new TrendPoint(p.At, p.CombatPower)).ToList();
        ItemLevelPoints = points.Select(p => new TrendPoint(p.At, p.ItemLevel)).ToList();
        HasTrend = points.Count > 1;

        var reference = points.FirstOrDefault();
        if (reference is not null && points.Count > 1)
        {
            string since = Loc.T("self.since_date", reference.At.ToString("d", Loc.Culture));
            (CombatPowerDelta, CombatPowerUp) = Delta(latest.CombatPower - reference.CombatPower, since);
            (ItemLevelDelta, ItemLevelUp) = Delta(latest.ItemLevel - reference.ItemLevel, since);
        }
        else
        {
            (CombatPowerDelta, CombatPowerUp, ItemLevelDelta, ItemLevelUp) = ("", false, "", false);
        }
        Caption = Loc.T(points.Count > 1 ? "self.caption" : "self.caption_first",
            latest.At.ToString("t", Loc.Culture), latest.At.Add(ProfileWatcher.Interval).ToString("t", Loc.Culture));

        Gear.Clear();
        foreach (var g in latest.Gear.Where(g => g.Icon is not null).Take(12)) Gear.Add(new GearIcon(g));

        if (latest.Portrait != shownPortrait)
        {
            shownPortrait = latest.Portrait;
            _ = RemoteImages.GetAsync(latest.Portrait).ContinueWith(t =>
            {
                if (t.Result is { } bitmap) Dispatcher.UIThread.Post(() => Portrait = bitmap);
            }, TaskScheduler.Default);
        }
    }

    public Func<double, string> NumberFormat { get; } = v => v.ToString("N0", Loc.Culture);

    private static (string Text, bool Up) Delta(int diff, string since) =>
        diff == 0 ? (Loc.T("self.unchanged", since), false)
        : ((diff > 0 ? "+" : "−") + Math.Abs(diff).ToString("N0", Loc.Culture) + " " + since, diff > 0);
}
