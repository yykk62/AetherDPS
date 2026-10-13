using Aion2Meter.App.Services;
using Aion2Meter.App.ViewModels;
using Avalonia;
using Avalonia.Controls;

namespace Aion2Meter.App.Views;

/// <summary>Buff-/Abklingzeit-Leiste – klebt unter dem Meter, lässt Mausklicks immer durch.</summary>
public partial class BuffBarWindow : Window
{
    public BuffBarWindow() => InitializeComponent();

    public BuffBarWindow(BuffBarViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Opened += (_, _) => ClickThrough.Set(this, true);
    }

    /// <summary>Direkt unter das Meter-Fenster setzen.</summary>
    public void FollowBelow(Window meter)
    {
        var scaling = meter.RenderScaling;
        Width = meter.Bounds.Width;
        Position = new PixelPoint(meter.Position.X, meter.Position.Y + (int)(meter.Bounds.Height * scaling) + 4);
    }
}
