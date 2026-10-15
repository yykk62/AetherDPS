using System.Collections.ObjectModel;
using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>
/// Timer-Overlay: jeder Timer ist eine eigene Pill. Pills stehen allein oder in Gruppen (<see cref="TimerGroup"/>),
/// die Fenster dazu verwaltet <see cref="Views.TimerPills"/>. Unabhängig vom DPS-Meter und STRG+W.
/// </summary>
public sealed partial class TimerOverlayViewModel : ObservableObject
{
    private readonly TimersViewModel timers;

    /// <summary>Alle Timer, die gerade im Overlay stehen sollen (nach Fälligkeit sortiert).</summary>
    public ObservableCollection<TimerRow> Rows { get; } = [];

    /// <summary>Welche Timer sichtbar sind, hat sich geändert (nicht nur die Restzeit).</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>„Bearbeiten“ → Hauptfenster, Reiter „Timer“.</summary>
    public event EventHandler? EditRequested;
    public event EventHandler? HideRequested;

    public TimerOverlayViewModel(TimersViewModel timers)
    {
        this.timers = timers;
        timers.Service.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);
        timers.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TimersViewModel.OverlayLocked)) LockChanged?.Invoke(this, EventArgs.Empty);
        };
        Rebuild();
    }

    public TimerService Service => timers.Service;

    /// <summary>Gesperrt: Klicks gehen durch die Pills ins Spiel. Wird gespeichert.</summary>
    public bool IsLocked
    {
        get => timers.OverlayLocked;
        set => timers.OverlayLocked = value;
    }

    public event EventHandler? LockChanged;

    /// <summary>Etwas anzuzeigen? (Modus „nur kurz vorher“ blendet sonst aus.)</summary>
    public bool HasContent => Rows.Count > 0;

    public TimerRow? Row(string name) => Rows.FirstOrDefault(r => r.Item.Name == name);

    private bool Visible(TimerItem item, DateTime now)
    {
        if (!item.ShowInOverlay) return false;
        if (timers.OverlayMode != "soon") return true;
        // Nur kurz vorher – und eine Minute danach, damit „jetzt“ noch zu sehen ist
        return item.DueAt is DateTime due && due - now <= TimeSpan.FromMinutes(timers.OverlaySoonMinutes) && now - due <= TimeSpan.FromMinutes(1);
    }

    private void Rebuild()
    {
        var now = DateTime.Now;
        var items = timers.Service.Items()
            .Where(i => Visible(i, now))
            .GroupBy(i => i.Name).Select(g => g.First())   // Name ist der Schlüssel der Pill
            .OrderBy(i => i.DueAt ?? DateTime.MaxValue)
            .ToList();
        var before = Rows.Select(r => r.Item.Name).ToHashSet();
        Rows.Clear();
        foreach (var item in items)
        {
            var row = new TimerRow(timers, item);
            row.Update(now);
            Rows.Add(row);
        }
        if (!before.SetEquals(items.Select(i => i.Name))) LayoutChanged?.Invoke(this, EventArgs.Empty);
        else RowsReplaced?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Gleiche Timer, neue Daten (z. B. neue Fälligkeit) – Pills neu binden, ohne Fenster umzubauen.</summary>
    public event EventHandler? RowsReplaced;

    public void Tick()
    {
        var now = DateTime.Now;
        if (timers.OverlayMode == "soon"
            || Rows.Any(r => r.Item.Kind == TimerKind.Schedule && r.Item.DueAt is DateTime due && due <= now))
        {
            Rebuild();
            return;
        }
        foreach (var row in Rows) row.Update(now);
    }

    /// <summary>Pill komplett entfernen (eigener Timer weg, fester Termin aus, Feldboss ohne Stern).</summary>
    public void RemoveTimer(TimerRow row) => Service.RemoveFromOverlay(row.Item.Kind, row.Item.Name);

    [RelayCommand]
    private void Edit() => EditRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Hide() => HideRequested?.Invoke(this, EventArgs.Empty);
}
