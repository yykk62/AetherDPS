using Aion2Meter.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Aion2Meter.App.Views;

/// <summary>Kleines Fenster der geführten Aufnahme – oben mittig, verschiebbar, über dem Spiel.</summary>
public partial class ResearchWindow : Window
{
    public ResearchWindow() => InitializeComponent();

    public ResearchWindow(ResearchViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        Opened += (_, _) =>
        {
            if (Screens.Primary is { } screen)
            {
                var area = screen.WorkingArea;
                Position = new PixelPoint(area.X + (area.Width - (int)(Bounds.Width * screen.Scaling)) / 2, area.Y + 40);
            }
        };
    }
}
