using AetherDPS.App.Services;
using AetherDPS.App.ViewModels;
using AetherDPS.Engine;
using Avalonia;
using Avalonia.Controls;

namespace AetherDPS.App.Views;

/// <summary>
/// Große Stagger-Warnung, standardmäßig oben mittig auf dem Hauptbildschirm – nie anklickbar. Die Stelle wird im
/// Platzierungsmodus festgelegt und gemerkt.
/// </summary>
public partial class StaggerWarningWindow : Window, IPlaceableOverlay
{
    private readonly MeterSettings? settings;
    private bool visibleBeforePlacement;

    public StaggerWarningWindow() => InitializeComponent();

    public StaggerWarningWindow(StaggerWarningViewModel viewModel, MeterSettings? settings = null) : this()
    {
        DataContext = viewModel;
        this.settings = settings;
        ClickThrough.NeverActivate(this);
        Opened += (_, _) =>
        {
            ClickThrough.Set(this, !placing);
            Place();
        };
    }

    private bool placing;

    private StaggerWarningViewModel ViewModel => (StaggerWarningViewModel)DataContext!;

    /// <summary>Gemerkte Stelle (ganz auf dem Bildschirm), sonst oben mittig auf dem Hauptbildschirm.</summary>
    private void Place()
    {
        if ((Screens.Primary ?? Screens.All.FirstOrDefault()) is not { } primary) return;
        if (settings?.StaggerLeft is double x && settings.StaggerTop is double y)
        {
            var at = new PixelPoint((int)x, (int)y);
            var screen = Screens.ScreenFromPoint(at) ?? primary;
            var area = screen.Bounds;
            var own = PixelSize.FromSize(ClientSize, screen.Scaling);
            Position = new PixelPoint(Math.Clamp(at.X, area.X, Math.Max(area.X, area.Right - own.Width)),
                Math.Clamp(at.Y, area.Y, Math.Max(area.Y, area.Bottom - own.Height)));
            return;
        }
        var work = primary.WorkingArea;
        var size = PixelSize.FromSize(ClientSize, primary.Scaling);
        Position = new PixelPoint(work.X + (work.Width - size.Width) / 2, work.Y + work.Height / 5);
    }

    public string? PlacementLabel => Loc.T("placement.stagger");

    public IEnumerable<Window> PlacementWindows => [this];

    /// <summary>Beispielphase (Leiste, Punkte, Restzeit), anklickbar.</summary>
    public void BeginPlacement()
    {
        placing = true;
        visibleBeforePlacement = IsVisible;
        ViewModel.ShowSample(true);
        if (!IsVisible) Show();
        ClickThrough.Set(this, false);
    }

    public void EndPlacement()
    {
        placing = false;
        if (settings is not null)
        {
            settings.StaggerLeft = Position.X;
            settings.StaggerTop = Position.Y;
            settings.Save();
        }
        ViewModel.ShowSample(false);
        ClickThrough.Set(this, true);
        if (!visibleBeforePlacement) Hide();
    }
}
