using AetherDPS.App.Services;
using AetherDPS.App.ViewModels;
using AetherDPS.Engine;
using AionDpsMeter.Services.Services.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;

namespace AetherDPS.App.Views;

public partial class MainWindow : Window, IPlaceableOverlay
{
    private readonly IAppSettingsService? settings;

    public MainWindow() => InitializeComponent();

    public MainWindow(MainViewModel viewModel, IAppSettingsService settings) : this()
    {
        DataContext = viewModel;
        this.settings = settings;
        RestoreBounds();
        ApplyOpacity();
        settings.SettingsChanged += (_, _) => Dispatcher.UIThread.Post(ApplyOpacity);

        // Verschieben läuft über den Platzierungsmodus (alle Overlays zusammen), das Schloss ist nur noch Klick-durch
        ClickThrough.NeverActivate(this);
        ClickThroughButton.IsCheckedChanged += (_, _) => SetClickThrough(ClickThroughButton.IsChecked == true);
        lockTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(60), DispatcherPriority.Input, (_, _) => UpdateLockHover());
        Closing += (_, _) => SaveBounds();
    }

    private readonly DispatcherTimer? lockTimer;

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    public bool IsClickThrough { get; private set; }

    /// <summary>
    /// Gesperrt = Klicks gehen ins Spiel. Nur das Schloss bleibt klickbar: Ein Timer
    /// schaut, ob die Maus darüber ist, und macht das Fenster solange wieder klickbar.
    /// </summary>
    public void SetClickThrough(bool enabled)
    {
        IsClickThrough = enabled;
        ClickThroughButton.IsChecked = enabled;
        ViewModel.IsClickThrough = enabled;
        if (enabled) lockTimer?.Start();
        else lockTimer?.Stop();
        ClickThrough.Set(this, enabled && !ClickThrough.IsCursorOver(ClickThroughButton));
    }

    private void UpdateLockHover()
    {
        if (!IsClickThrough) return;
        ClickThrough.Set(this, !ClickThrough.IsCursorOver(ClickThroughButton));
    }

    /// <summary>✕: Die App entscheidet (Bereitschaft oder im Auto-Modus nur ausblenden).</summary>
    public event EventHandler? CloseRequested;

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Knopf „Overlays platzieren“ (nur unter der Maus sichtbar).</summary>
    public event EventHandler? PlacementRequested;

    private void OnPlaceClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        PlacementRequested?.Invoke(this, EventArgs.Empty);

    private bool visibleBeforePlacement;

    public string? PlacementLabel => Loc.T("placement.combat");

    public IEnumerable<Window> PlacementWindows => [this];

    /// <summary>Beispielkampf in echter Größe, klickbar (auch wenn gesperrt), sichtbar.</summary>
    public void BeginPlacement()
    {
        visibleBeforePlacement = IsVisible;
        ViewModel.ShowSample(true);
        lockTimer?.Stop();
        if (!IsVisible) Show();
        ClickThrough.Set(this, false);
    }

    public void EndPlacement()
    {
        SaveBounds();
        ViewModel.ShowSample(false);
        SetClickThrough(IsClickThrough);
        if (!visibleBeforePlacement) Hide();
    }

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: PlayerRow row })
            ViewModel.SelectPlayerCommand.Execute(row);
    }

    private void ApplyOpacity()
    {
        // Nur der Hintergrund wird durchsichtig – Text und Balken bleiben voll lesbar.
        byte alpha = (byte)Math.Round(Math.Clamp(settings?.WindowOpacity ?? 0.9, 0.2, 1.0) * 255);
        var basis = Avalonia.Application.Current?.TryGetResource("Stone950", null, out var c) == true && c is Avalonia.Media.Color color
            ? color : Avalonia.Media.Colors.Black;   // Token „tiefste Mulde“
        Root.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(alpha, basis.R, basis.G, basis.B));
    }

    private void RestoreBounds()
    {
        if (settings is null) return;
        // Die Höhe ergibt sich aus dem Inhalt (Mini-Leiste ↔ Kampf), nur die Breite ist einstellbar.
        if (settings.WindowWidth is double w && w >= MinWidth) Width = w;
        if (settings.WindowLeft is double x && settings.WindowTop is double y)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint((int)x, (int)y);
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public void SaveBounds()
    {
        if (settings is null) return;
        settings.WindowLeft = Position.X;
        settings.WindowTop = Position.Y;
        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Rechten Rand ziehen = breiter/schmaler (Fenster hat keine Systemränder)
        var p = e.GetPosition(this);
        if (p.X >= Bounds.Width - 8) BeginResizeDrag(WindowEdge.East, e);
    }
}
