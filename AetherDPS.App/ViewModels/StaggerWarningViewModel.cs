using AetherDPS.App.Services;
using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AetherDPS.App.ViewModels;

/// <summary>Große Stagger-Warnung: Boss, Leiste und geschätzte Restzeit.</summary>
public sealed partial class StaggerWarningViewModel(StaggerTracker stagger) : ObservableObject
{
    [ObservableProperty] private bool isActive;
    [ObservableProperty] private string boss = "";
    [ObservableProperty] private double percent;
    [ObservableProperty] private string points = "";
    [ObservableProperty] private string timeLeft = "";
    [ObservableProperty] private bool hurry;

    /// <summary>Alle 200 ms aufrufen.</summary>
    public void Refresh()
    {
        if (stagger.Current is not { } st)
        {
            IsActive = false;
            return;
        }
        IsActive = true;
        Boss = st.Boss;
        Percent = st.Max > 0 ? st.Current * 100.0 / st.Max : 0;
        Points = Loc.T("warning.points", st.Current, st.Max);
        var left = st.ExpectedEnd - DateTime.Now;
        TimeLeft = left > TimeSpan.Zero ? Loc.T("warning.seconds", (int)Math.Ceiling(left.TotalSeconds)) : "";
        Hurry = left > TimeSpan.Zero && left < TimeSpan.FromSeconds(5);
    }
}
