using System.Collections.ObjectModel;
using Aion2Meter.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Meter.App.ViewModels;

/// <summary>
/// Timer-Overlay: eigenes, frei verschiebbares Fenster (unabhängig vom DPS-Meter und STRG+W).
/// Zeigt alle Timer mit „im Overlay“ – nächster Termin zuerst.
/// </summary>
public sealed partial class TimerOverlayViewModel : ObservableObject
{
    private const int MaxRows = 14;
    private readonly TimersViewModel timers;

    public ObservableCollection<TimerRow> Rows { get; } = [];

    [ObservableProperty] private bool hasRows;
    [ObservableProperty] private bool isClickThrough;

    /// <summary>„+“ oder Klick auf einen Timer → Hauptfenster, Reiter „Timer“.</summary>
    public event EventHandler? EditRequested;
    public event EventHandler? HideRequested;

    public TimerOverlayViewModel(TimersViewModel timers)
    {
        this.timers = timers;
        timers.Service.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);
        Rebuild();
    }

    private void Rebuild()
    {
        Rows.Clear();
        foreach (var item in timers.Service.Items()
                     .Where(i => i.ShowInOverlay)
                     .OrderBy(i => i.DueAt ?? DateTime.MaxValue)
                     .Take(MaxRows))
            Rows.Add(new TimerRow(timers, item));
        HasRows = Rows.Count > 0;
        Tick();
    }

    public void Tick()
    {
        var now = DateTime.Now;
        if (Rows.Any(r => r.Item.Kind == TimerKind.Schedule && r.Item.DueAt is DateTime due && due <= now))
        {
            Rebuild();
            return;
        }
        foreach (var row in Rows) row.Update(now);
    }

    [RelayCommand]
    private void Edit() => EditRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Hide() => HideRequested?.Invoke(this, EventArgs.Empty);
}
