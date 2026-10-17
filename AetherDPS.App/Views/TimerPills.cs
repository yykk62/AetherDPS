using AetherDPS.App.Services;
using AetherDPS.App.ViewModels;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace AetherDPS.App.Views;

/// <summary>
/// Ein Fenster je Gruppe (auch eine einzelne Pill ist eine Gruppe). Durchsichtig, nur die Pills sind zu sehen;
/// Ziehen an einer Pill verschiebt die ganze Gruppe.
/// </summary>
public sealed class TimerGroupWindow : Window
{
    private readonly Canvas canvas = new();
    public TimerGroup Group { get; private set; }
    public event Action<TimerGroupWindow>? Moved;

    public TimerGroupWindow(TimerGroup group)
    {
        Group = group;
        Title = Loc.T("timer_overlay.title");
        SystemDecorations = SystemDecorations.None;
        Topmost = true;
        CanResize = false;
        ShowActivated = false;
        ShowInTaskbar = false;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Content = canvas;
        Position = new PixelPoint((int)group.X, (int)group.Y);
        PositionChanged += (_, _) => Moved?.Invoke(this);
        ClickThrough.NeverActivate(this);
    }

    public IReadOnlyList<TimerPill> Pills => canvas.Children.OfType<TimerPill>().ToList();

    /// <summary>Pills neu setzen (nur sichtbare Timer; leere Zellen bleiben frei, damit nichts springt).</summary>
    public void Layout(TimerGroup group, IReadOnlyList<(TimerCell Cell, TimerRow Row)> pills, Func<TimerPill, ContextMenu> menu,
        Action<TimerGroupWindow, PointerPressedEventArgs> pressed)
    {
        Group = group;
        canvas.Children.Clear();
        int cols = pills.Count == 0 ? 1 : pills.Max(p => p.Cell.Col) + 1;
        int rows = pills.Count == 0 ? 1 : pills.Max(p => p.Cell.Row) + 1;
        canvas.Width = cols * TimerPill.PillWidth + (cols - 1) * TimerPill.Gap;
        canvas.Height = rows * TimerPill.PillHeight + (rows - 1) * TimerPill.Gap;
        foreach (var (cell, row) in pills)
        {
            var pill = new TimerPill { Key = cell.Name, DataContext = row };
            Canvas.SetLeft(pill, cell.Col * (TimerPill.PillWidth + TimerPill.Gap));
            Canvas.SetTop(pill, cell.Row * (TimerPill.PillHeight + TimerPill.Gap));
            pill.ContextMenu = menu(pill);
            pill.PointerPressed += (_, e) => pressed(this, e);
            canvas.Children.Add(pill);
        }
    }

    /// <summary>Neue Daten, gleiche Timer: nur neu binden.</summary>
    public void Rebind(Func<string, TimerRow?> row)
    {
        foreach (var pill in Pills)
            if (row(pill.Key) is { } r) pill.DataContext = r;
    }

    /// <summary>Bildschirm-Rechteck einer Pill (physische Pixel).</summary>
    public PixelRect RectOf(TimerPill pill)
    {
        double scaling = RenderScaling;
        var x = Canvas.GetLeft(pill) * scaling;
        var y = Canvas.GetTop(pill) * scaling;
        return new PixelRect(Position.X + (int)x, Position.Y + (int)y, (int)(TimerPill.PillWidth * scaling), (int)(TimerPill.PillHeight * scaling));
    }

    public void SetClickThrough(bool enabled) => ClickThrough.Set(this, enabled);
}

/// <summary>
/// Steuert alle Timer-Pills: jede sichtbare Pill gehört zu genau einer Gruppe (sonst bekommt sie eine eigene),
/// Rechtsklick: gruppieren (Raster aus der Lage, siehe <see cref="PillLayout"/>), lösen, Spalten, entfernen, sperren.
/// Verschoben wird im gemeinsamen Platzierungsmodus (<see cref="OverlayPlacement"/>); dort stehen alle Pills des
/// Overlays, auch die, die im Modus „nur kurz vorher“ gerade ausgeblendet sind.
/// </summary>
public sealed class TimerPills : IPlaceableOverlay
{
    private readonly TimerOverlayViewModel vm;
    private readonly TimerService service;
    private readonly Dictionary<TimerGroup, TimerGroupWindow> windows = [];
    private readonly DispatcherTimer saveTimer;
    private bool shown;
    private bool placing, shownBeforePlacement;

    /// <summary>Fenster gebaut oder geschlossen (z. B. beim Gruppieren im Platzierungsmodus).</summary>
    public event Action? WindowsChanged;

    /// <summary>Aus dem Menü einer Pill: „Overlays platzieren“.</summary>
    public event EventHandler? PlacementRequested;

    public TimerPills(TimerOverlayViewModel vm, TimerService service)
    {
        this.vm = vm;
        this.service = service;
        vm.LayoutChanged += (_, _) => Sync();
        vm.RowsReplaced += (_, _) =>
        {
            foreach (var w in windows.Values) w.Rebind(RowOf);
        };
        vm.LockChanged += (_, _) => ApplyLock();
        saveTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            saveTimer!.Stop();
            service.SaveQuiet();
        });
    }

    public bool HasContent => vm.HasContent;
    public IReadOnlyCollection<TimerGroupWindow> Windows => windows.Values;

    public void ShowAll()
    {
        shown = true;
        Sync();
    }

    public void HideAll()
    {
        shown = false;
        foreach (var w in windows.Values) w.Hide();
    }

    public void SaveBounds() => service.SaveQuiet();

    public string? PlacementLabel => null;

    public IEnumerable<Window> PlacementWindows => windows.Values.Where(w => w.IsVisible);

    public void BeginPlacement()
    {
        shownBeforePlacement = shown;
        placing = true;
        shown = true;
        Sync();
        foreach (var w in windows.Values) w.SetClickThrough(false);
    }

    public void EndPlacement()
    {
        placing = false;
        shown = shownBeforePlacement;
        foreach (var w in windows.Values.Where(w => w.IsVisible)) OnMoved(w);
        service.SaveQuiet();
        Sync();
    }

    /// <summary>Name → Zeile: was gerade im Overlay steht, im Platzierungsmodus auch die ausgeblendeten Pills.</summary>
    private TimerRow? RowOf(string name) => placing ? vm.PlacementRow(name) : vm.Row(name);

    private List<TimerGroup> Groups => service.Settings.Groups;

    /// <summary>Gruppen an die sichtbaren Timer anpassen und Fenster bauen/verstecken.</summary>
    public void Sync()
    {
        Migrate();
        var visible = vm.Rows.Select(r => r.Item.Name).ToHashSet();
        if (placing) visible.UnionWith(Groups.SelectMany(g => g.Cells).Select(c => c.Name).Where(n => vm.PlacementRow(n) is not null));
        bool changed = false;
        foreach (var name in visible.Where(n => !Groups.Any(g => g.Cells.Any(c => c.Name == n))).ToList())
        {
            var (x, y) = FreeSpot();
            Groups.Add(new TimerGroup { X = x, Y = y, Cells = [new TimerCell(name, 0, 0)] });
            changed = true;
        }
        if (changed) service.SaveQuiet();

        foreach (var g in windows.Keys.Where(g => !Groups.Contains(g)).ToList())
        {
            windows[g].Close();
            windows.Remove(g);
        }
        foreach (var group in Groups)
        {
            var pills = group.Cells.Where(c => visible.Contains(c.Name)).Select(c => (c, RowOf(c.Name)!)).ToList();
            if (!windows.TryGetValue(group, out var window))
            {
                window = new TimerGroupWindow(group);
                window.Moved += OnMoved;
                windows[group] = window;
            }
            window.Layout(group, pills, Menu, OnPressed);
            if (shown && pills.Count > 0)
            {
                if (!window.IsVisible) window.Show();
                window.SetClickThrough(vm.IsLocked && !placing);
            }
            else if (window.IsVisible) window.Hide();
        }
        WindowsChanged?.Invoke();
    }

    /// <summary>Alte Anordnung (Gruppe + gelöste Leisten, bis 0.7.0) einmalig in Pills übernehmen.</summary>
    private void Migrate()
    {
        var s = service.Settings;
        if (s.PillsVersion >= 1) return;
        s.PillsVersion = 1;
        foreach (var (name, pos) in s.Detached)
            if (pos is [var x, var y]) Groups.Add(new TimerGroup { X = x, Y = y, Cells = [new TimerCell(name, 0, 0)] });
        var rest = vm.Rows.Select(r => r.Item.Name).Where(n => !s.Detached.ContainsKey(n)).ToList();
        if (rest.Count > 0 && s.OverlayLeft is double left && s.OverlayTop is double top)
            Groups.Add(new TimerGroup { X = left, Y = top, Cells = PillLayout.Grid(rest, Math.Clamp(s.OverlayColumns, 1, 4)) });
        s.Detached.Clear();
        service.SaveQuiet();
    }

    /// <summary>Platz für eine neue Pill: rechts oben, unter den vorhandenen.</summary>
    private (double X, double Y) FreeSpot()
    {
        var screen = windows.Values.FirstOrDefault()?.Screens.Primary;
        double scaling = screen?.Scaling ?? 1;
        var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1040);
        double w = TimerPill.PillWidth * scaling, h = (TimerPill.PillHeight + TimerPill.Gap) * scaling;
        double x = area.Right - w - 24 * scaling, y = area.Y + 120 * scaling;
        while (Occupied(x, y, w, h) && y < area.Bottom - h) y += h;
        return (x, y);
    }

    private bool Occupied(double x, double y, double w, double h) =>
        windows.Values.Where(v => v.IsVisible).SelectMany(v => v.Pills.Select(v.RectOf))
            .Any(r => r.X < x + w && x < r.Right && r.Y < y + h && y < r.Bottom)
        || Groups.Any(g => !windows.ContainsKey(g) && Math.Abs(g.X - x) < w && Math.Abs(g.Y - y) < h);

    private void OnMoved(TimerGroupWindow window)
    {
        window.Group.X = window.Position.X;
        window.Group.Y = window.Position.Y;
        saveTimer.Stop();
        saveTimer.Start();
    }

    /// <summary>Linksklick auf eine Pill tut nichts mehr: Verschieben läuft über den Platzierungsmodus.</summary>
    private void OnPressed(TimerGroupWindow window, PointerPressedEventArgs e)
    {
    }

    private ContextMenu Menu(TimerPill pill)
    {
        var menu = new ContextMenu();
        menu.Opening += (_, _) => Fill(menu, pill);
        return menu;
    }

    /// <summary>Menü erst beim Öffnen füllen – die Gruppe kann sich seitdem geändert haben.</summary>
    private void Fill(ContextMenu menu, TimerPill pill)
    {
        var window = windows.Values.FirstOrDefault(w => w.Pills.Contains(pill));
        if (window is null) return;
        bool inGroup = window.Group.Cells.Count > 1;
        var items = new List<Control>
        {
            Item(Loc.T("timer_pill.group"), () => GroupAround(window)),
        };
        if (inGroup)
        {
            items.Add(Item(Loc.T("timer_pill.ungroup"), () => Ungroup(window, pill)));
            items.Add(Item(Loc.T("timer_pill.split"), () => Split(window)));
            var columns = new MenuItem { Header = Loc.T("timer_overlay.columns") };
            var cols = new List<MenuItem>();
            for (int n = 1; n <= 4; n++)
            {
                int count = n;
                cols.Add(Item(n.ToString(), () => SetColumns(window, count)));
            }
            columns.ItemsSource = cols;
            items.Add(columns);
        }
        items.Add(new Separator());
        items.Add(Item(Loc.T("timer_overlay.edit"), () => vm.EditCommand.Execute(null)));
        items.Add(Item(Loc.T("timer_pill.remove"), () =>
        {
            if (vm.Row(pill.Key) is { } row) vm.RemoveTimer(row);
        }));
        items.Add(new Separator());
        if (!placing) items.Add(Item(Loc.T("placement.title"), () => PlacementRequested?.Invoke(this, EventArgs.Empty)));
        items.Add(Item(Loc.T("timer_pill.lock"), () => vm.IsLocked = true));
        items.Add(Item(Loc.T("timer_pill.hide_all"), () => vm.HideCommand.Execute(null)));
        menu.ItemsSource = items;
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => Dispatcher.UIThread.Post(action);
        return item;
    }

    /// <summary>
    /// Gruppieren: alle Pills, die (über Nachbarn) nah an dieser Gruppe liegen, werden eine Gruppe. Das Raster kommt aus
    /// ihrer Lage – nebeneinander, untereinander oder als Block –, ausgerichtet an der Pill ganz oben links.
    /// </summary>
    public void GroupAround(TimerGroupWindow start)
    {
        var visible = windows.Values.Where(w => w.IsVisible && w.Pills.Count > 0).ToList();
        if (!visible.Contains(start)) return;
        double scaling = start.RenderScaling;
        int reach = (int)(TimerPill.PillHeight * scaling);   // höchstens eine Pill-Höhe Abstand
        var cluster = new List<TimerGroupWindow> { start };
        for (int i = 0; i < cluster.Count; i++)
            foreach (var other in visible.Except(cluster).ToList())
                if (Near(cluster[i], other, reach)) cluster.Add(other);

        var pills = cluster.SelectMany(w => w.Pills.Select(p => (Pill: p, Rect: w.RectOf(p)))).ToList();
        var cells = PillLayout.Infer(pills.Select(p => (p.Pill.Key, (double)p.Rect.Center.X, (double)p.Rect.Center.Y)).ToList(),
            TimerPill.PillWidth * scaling, TimerPill.PillHeight * scaling);
        // Ursprung: Zelle (0,0) liegt auf der linken Kante der Spalte 0 und der oberen Kante der Zeile 0
        var byName = pills.ToDictionary(p => p.Pill.Key, p => p.Rect);
        int x = cells.Where(c => c.Col == 0).Min(c => byName[c.Name].X);
        int y = cells.Where(c => c.Row == 0).Min(c => byName[c.Name].Y);
        // ausgeblendete Mitglieder (Modus „kurz vorher“) behalten einen Platz hinten in der Gruppe
        int nextRow = cells.Max(c => c.Row) + 1;
        foreach (var hidden in cluster.SelectMany(w => w.Group.Cells).Where(c => !byName.ContainsKey(c.Name)))
            cells.Add(new TimerCell(hidden.Name, 0, nextRow++));

        foreach (var w in cluster) Groups.Remove(w.Group);
        Groups.Add(new TimerGroup { X = x, Y = y, Cells = cells });
        service.SaveQuiet();
        Sync();
    }

    private static bool Near(TimerGroupWindow a, TimerGroupWindow b, int reach) =>
        a.Pills.Any(p => b.Pills.Any(q =>
        {
            var r1 = a.RectOf(p);
            var r2 = b.RectOf(q);
            int dx = Math.Max(0, Math.Max(r1.X - r2.Right, r2.X - r1.Right));
            int dy = Math.Max(0, Math.Max(r1.Y - r2.Bottom, r2.Y - r1.Bottom));
            return dx <= reach && dy <= reach;
        }));

    /// <summary>Eine Pill aus der Gruppe nehmen – sie erscheint rechts neben der Gruppe auf ihrer Zeile.</summary>
    private void Ungroup(TimerGroupWindow window, TimerPill pill)
    {
        var rect = window.RectOf(pill);
        var group = window.Group;
        group.Cells.RemoveAll(c => c.Name == pill.Key);
        double right = window.Position.X + window.Bounds.Width * window.RenderScaling + TimerPill.Gap * 2 * window.RenderScaling;
        Groups.Add(new TimerGroup { X = right, Y = rect.Y, Cells = [new TimerCell(pill.Key, 0, 0)] });
        Compact(group);
        service.SaveQuiet();
        Sync();
    }

    /// <summary>Gruppe auflösen: jede Pill bleibt genau dort stehen, wo sie gerade ist.</summary>
    private void Split(TimerGroupWindow window)
    {
        var rects = window.Pills.ToDictionary(p => p.Key, window.RectOf);
        Groups.Remove(window.Group);
        foreach (var cell in window.Group.Cells)
        {
            var (x, y) = rects.TryGetValue(cell.Name, out var r) ? (r.X, r.Y) : ((int)window.Group.X, (int)window.Group.Y);
            Groups.Add(new TimerGroup { X = x, Y = y, Cells = [new TimerCell(cell.Name, 0, 0)] });
        }
        service.SaveQuiet();
        Sync();
    }

    private void SetColumns(TimerGroupWindow window, int columns)
    {
        var order = window.Group.Cells.OrderBy(c => c.Row).ThenBy(c => c.Col).Select(c => c.Name).ToList();
        window.Group.Cells = PillLayout.Grid(order, columns);
        service.SaveQuiet();
        Sync();
    }

    /// <summary>Nach dem Herausnehmen: leere Spalten/Zeilen schließen.</summary>
    private static void Compact(TimerGroup group)
    {
        var cols = group.Cells.Select(c => c.Col).Distinct().Order().ToList();
        var rows = group.Cells.Select(c => c.Row).Distinct().Order().ToList();
        group.Cells = group.Cells.Select(c => c with { Col = cols.IndexOf(c.Col), Row = rows.IndexOf(c.Row) }).ToList();
    }

    private void ApplyLock()
    {
        foreach (var w in windows.Values) w.SetClickThrough(vm.IsLocked && !placing);
    }
}
