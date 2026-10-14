using Aion2Meter.App.Services;
using Aion2Meter.App.ViewModels;
using Avalonia;
using Avalonia.Controls;

namespace Aion2Meter.App.Views;

/// <summary>Große Stagger-Warnung oben mittig auf dem Hauptbildschirm – nie anklickbar.</summary>
public partial class StaggerWarningWindow : Window
{
    public StaggerWarningWindow() => InitializeComponent();

    public StaggerWarningWindow(StaggerWarningViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Opened += (_, _) =>
        {
            ClickThrough.Set(this, true);
            if (Screens.Primary is { } screen)
            {
                var area = screen.WorkingArea;
                var size = PixelSize.FromSize(Bounds.Size, screen.Scaling);
                Position = new PixelPoint(area.X + (area.Width - size.Width) / 2, area.Y + area.Height / 5);
            }
        };
    }
}
