using AetherDPS.App.Services;
using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AetherDPS.App.ViewModels;

/// <summary>Stagger-Warnung mitten im Bild: Leiste und geschätzte Restzeit – nur bei Phasen mit Zeitlimit (Wipe-Mechanik).</summary>
public sealed partial class StaggerWarningViewModel(StaggerTracker stagger) : ObservableObject
{
    [ObservableProperty] private bool isActive;
    [ObservableProperty] private string boss = "";
    [ObservableProperty] private double percent;
    [ObservableProperty] private string points = "";
    [ObservableProperty] private string timeLeft = "";
    [ObservableProperty] private bool hurry;

    private bool sample;

    /// <summary>Platzierungsmodus: Beispielphase statt der echten (Refresh lässt sie dann stehen).</summary>
    public void ShowSample(bool on)
    {
        sample = on;
        if (!on)
        {
            Refresh();
            return;
        }
        IsActive = true;
        Boss = Loc.T("placement.sample_boss");
        Percent = 62;
        Points = Loc.T("warning.points", 196, 317);
        TimeLeft = Loc.T("warning.seconds", 12);
        Hurry = false;
    }

    /// <summary>Alle 200 ms aufrufen.</summary>
    public void Refresh()
    {
        if (sample) return;
        // Dauer-Leisten (Schild-Gegner) zeigt nur die Boss-Karte
        if (stagger.Current is not { ExpectedEnd: { } end } st)
        {
            IsActive = false;
            return;
        }
        IsActive = true;
        Boss = st.Boss;
        Percent = st.Max > 0 ? st.Current * 100.0 / st.Max : 0;
        Points = Loc.T("warning.points", st.Current, st.Max);
        var left = end - DateTime.Now;
        TimeLeft = left > TimeSpan.Zero ? Loc.T("warning.seconds", (int)Math.Ceiling(left.TotalSeconds)) : "";
        Hurry = left > TimeSpan.Zero && left < TimeSpan.FromSeconds(5);
    }
}
