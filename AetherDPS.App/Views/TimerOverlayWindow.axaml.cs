using System.Collections.Specialized;
using AetherDPS.App.Services;
using AetherDPS.App.ViewModels;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AetherDPS.App.Views;

/// <summary>
/// Timer-Gruppe: verschiebbar (Kopf ziehen), Höhe passt sich an, sperrbar wie der DPS-Meter
/// (Schloss bleibt klickbar). Einzeln gelöste Timer bekommen je eine eigene Leiste
/// (<see cref="TimerBarWindow"/>), die mit der Gruppe ein- und ausgeblendet wird.
/// </summary>
public partial class TimerOverlayWindow : Window
{
    private readonly TimerSettings? settings;
    private readonly TimerService? service;
    private readonly DispatcherTimer? lockTimer;
    private readonly DispatcherTimer? tickTimer;
    private readonly Dictionary<string, TimerBarWindow> bars = [];
    private bool shown;

    public TimerOverlayWindow() => InitializeComponent();

    public TimerOverlayWindow(TimerOverlayViewModel viewModel, TimerService service) : this()
    {
        DataContext = viewModel;
        this.service = service;
        settings = service.Settings;
        RestorePosition();

        Header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        LockButton.IsCheckedChanged += (_, _) => SetClickThrough(LockButton.IsChecked == true);
        lockTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(60), DispatcherPriority.Input, (_, _) =>
        {
            if (viewModel.IsClickThrough) ClickThrough.Set(this, !ClickThrough.IsCursorOver(LockButton));
        });
        tickTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => viewModel.Tick());
        Opened += (_, _) => tickTimer.Start();
        Closing += (_, _) => SaveBounds();
        PositionChanged += (_, _) => ScheduleSave();
        NotifyCollectionChangedEventHandler onDetached = (_, _) => SyncBars();
        viewModel.Detached.CollectionChanged += onDetached;
        Closed += (_, _) =>
        {
            viewModel.Detached.CollectionChanged -= onDetached;
            foreach (var bar in bars.Values) bar.Close();
            bars.Clear();
        };
    }

    private TimerOverlayViewModel ViewModel => (TimerOverlayViewModel)DataContext!;

    public bool HasContent => ViewModel.HasContent;

    /// <summary>Losgelassene Kachel über der Gruppe? Dann zurück in die Gruppe (Andocken).</summary>
    public bool TryDock(TimerBarWindow bar)
    {
        if (!IsVisible) return false;
        var scaling = Screens.ScreenFromWindow(this)?.Scaling ?? 1;
        var group = new PixelRect(Position, PixelSize.FromSize(Bounds.Size, scaling));
        var tile = new PixelRect(bar.Position, PixelSize.FromSize(bar.Bounds.Size, scaling));
        var center = tile.Center;
        var area = new PixelRect(group.X - 12, group.Y - 12, group.Width + 24, group.Height + 24);
        if (!area.Contains(center)) return false;
        if (bar.DataContext is TimerRow row) service?.SetDetached(row.Item.Name, false);
        return true;
    }

    /// <summary>Gruppe und alle gelösten Leisten zeigen.</summary>
    public void ShowAll()
    {
        shown = true;
        if (!IsVisible) Show();
        SyncBars();
    }

    /// <summary>Gruppe und alle gelösten Leisten ausblenden.</summary>
    public void HideAll()
    {
        shown = false;
        SaveBounds();
        Hide();
        foreach (var bar in bars.Values) bar.Hide();
    }

    private void SyncBars()
    {
        if (!shown) return;   // ausgeblendet: Leisten entstehen beim nächsten ShowAll
        var wanted = ViewModel.Detached.ToDictionary(r => r.Item.Name);
        foreach (var name in bars.Keys.Where(n => !wanted.ContainsKey(n)).ToList())
        {
            bars[name].Close();
            bars.Remove(name);
        }
        int index = 0;
        foreach (var (name, row) in wanted)
        {
            if (!bars.TryGetValue(name, out var bar))
            {
                bar = new TimerBarWindow(row, service!, ViewModel, DefaultBarPosition(index), TryDock);
                bars[name] = bar;
            }
            else bar.DataContext = row;
            bar.SetClickThrough(ViewModel.IsClickThrough);
            if (shown && !bar.IsVisible) bar.Show();
            index++;
        }
    }

    /// <summary>Neue Leisten erscheinen untereinander links neben der Gruppe.</summary>
    private PixelPoint DefaultBarPosition(int index)
    {
        int x = Position.X - 160;
        if (Screens.ScreenFromWindow(this) is { } screen && x < screen.WorkingArea.X)
            x = Position.X + (int)(Bounds.Width * screen.Scaling) + 10;   // links kein Platz → rechts daneben
        return new PixelPoint(x, Position.Y + index * 60);
    }

    private void SetClickThrough(bool enabled)
    {
        ViewModel.IsClickThrough = enabled;
        LockButton.IsChecked = enabled;
        if (enabled) lockTimer?.Start();
        else lockTimer?.Stop();
        ClickThrough.Set(this, enabled && !ClickThrough.IsCursorOver(LockButton));
        foreach (var bar in bars.Values) bar.SetClickThrough(enabled);
    }

    private void OnRowReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left) ViewModel.EditCommand.Execute(null);
    }

    private void OnEditClicked(object? sender, RoutedEventArgs e) => ViewModel.EditCommand.Execute(null);

    private DispatcherTimer? saveTimer;

    private void ScheduleSave()
    {
        saveTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            saveTimer!.Stop();
            SaveBounds();
        });
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void RestorePosition()
    {
        if (settings is null) return;
        if (settings.OverlayLeft is double x && settings.OverlayTop is double y)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint((int)x, (int)y);
        }
        else if (Screens.Primary is { } screen)
        {
            // Standard: rechts oben auf dem Hauptbildschirm
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(screen.WorkingArea.Right - (int)(Width * screen.Scaling) - 24, screen.WorkingArea.Y + 120);
        }
    }

    public void SaveBounds()
    {
        if (settings is null || service is null || !IsVisible) return;
        settings.OverlayLeft = Position.X;
        settings.OverlayTop = Position.Y;
        service.SaveQuiet();
    }
}
