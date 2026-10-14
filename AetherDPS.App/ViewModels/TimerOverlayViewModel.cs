using System.Collections.ObjectModel;
using AetherDPS.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>
/// Timer-Overlay: eine schmale Gruppe (passt sich der Anzahl an) plus einzeln gelöste Timer,
/// die als eigene Leisten frei verteilt werden können. Unabhängig vom DPS-Meter und STRG+W.
/// </summary>
public sealed partial class TimerOverlayViewModel : ObservableObject
{
    private const int MaxRows = 12;
    private readonly TimersViewModel timers;

    /// <summary>Timer in der Gruppe.</summary>
    public ObservableCollection<TimerRow> Rows { get; } = [];

    /// <summary>Einzeln gelöste Timer – je eine eigene Leiste.</summary>
    public ObservableCollection<TimerRow> Detached { get; } = [];

    [ObservableProperty] private bool hasRows;
    [ObservableProperty] private bool isClickThrough;

    /// <summary>„+“ oder Klick auf einen Timer → Hauptfenster, Reiter „Timer“.</summary>
    public event EventHandler? EditRequested;
    public event EventHandler? HideRequested;

    public TimerOverlayViewModel(TimersViewModel timers)
    {
        this.timers = timers;
        columns = Math.Clamp(timers.Service.Settings.OverlayColumns, 1, 4);
        timers.Service.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);
        Rebuild();
    }

    public TimerService Service => timers.Service;

    /// <summary>Spalten der Gruppe (1–4), per Rechtsklick auf den Kopf.</summary>
    [ObservableProperty] private int columns;

    partial void OnColumnsChanged(int value)
    {
        if (timers.Service.Settings.OverlayColumns == value) return;
        timers.Service.Settings.OverlayColumns = value;
        timers.Service.SaveQuiet();
    }

    [RelayCommand]
    private void SetColumns(string count) => Columns = Math.Clamp(int.Parse(count), 1, 4);

    /// <summary>Alle Timer als einzelne Kacheln lösen.</summary>
    [RelayCommand]
    private void DetachAll()
    {
        foreach (var row in Rows.ToList()) timers.Service.SetDetached(row.Item.Name, true);
    }

    /// <summary>Alle einzelnen Kacheln zurück in die Gruppe.</summary>
    [RelayCommand]
    private void AttachAll()
    {
        foreach (var row in Detached.ToList()) timers.Service.SetDetached(row.Item.Name, false);
    }

    /// <summary>Etwas anzuzeigen? (Modus „nur kurz vorher“ blendet sonst aus.)</summary>
    public bool HasContent => Rows.Count > 0 || Detached.Count > 0;

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
            .OrderBy(i => i.DueAt ?? DateTime.MaxValue)
            .ToList();
        Sync(Rows, items.Where(i => !timers.Service.IsDetached(i.Name)).Take(MaxRows));
        Sync(Detached, items.Where(i => timers.Service.IsDetached(i.Name)));
        HasRows = Rows.Count > 0;
        UpdateRows(now);
    }

    private void UpdateRows(DateTime now)
    {
        foreach (var row in Rows) row.Update(now);
        foreach (var row in Detached) row.Update(now);
    }

    /// <summary>Liste nur ändern, wo sich etwas geändert hat – sonst flackern die Leisten-Fenster.</summary>
    private void Sync(ObservableCollection<TimerRow> target, IEnumerable<TimerItem> items)
    {
        var wanted = items.ToList();
        for (int i = target.Count - 1; i >= 0; i--)
            if (!wanted.Any(w => w.Name == target[i].Item.Name && w.Kind == target[i].Item.Kind))
                target.RemoveAt(i);
        for (int i = 0; i < wanted.Count; i++)
        {
            int existing = -1;
            for (int j = 0; j < target.Count; j++)
                if (target[j].Item.Name == wanted[i].Name && target[j].Item.Kind == wanted[i].Kind) existing = j;
            var row = new TimerRow(timers, wanted[i]);
            if (existing < 0) target.Insert(Math.Min(i, target.Count), row);
            else if (target[existing].Item.DueAt != wanted[i].DueAt || target[existing].Item.Detail != wanted[i].Detail) target[existing] = row;
            else if (existing != i && i < target.Count) target.Move(existing, i);
        }
    }

    public void Tick()
    {
        var now = DateTime.Now;
        if (timers.OverlayMode == "soon"
            || Rows.Concat(Detached).Any(r => r.Item.Kind == TimerKind.Schedule && r.Item.DueAt is DateTime due && due <= now))
        {
            Rebuild();
            return;
        }
        UpdateRows(now);
    }

    [RelayCommand]
    private void Edit() => EditRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Hide() => HideRequested?.Invoke(this, EventArgs.Empty);
}
