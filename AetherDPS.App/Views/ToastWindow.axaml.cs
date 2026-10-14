using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AetherDPS.App.Views;

/// <summary>Erinnerung unten rechts (ohne Ton, ohne Fokus zu stehlen), verschwindet nach 8 s.</summary>
public partial class ToastWindow : Window
{
    private readonly DispatcherTimer hideTimer;

    public ToastWindow()
    {
        InitializeComponent();
        hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            Hide();
        };
        PointerPressed += (_, _) => Hide();
    }

    public void ShowMessage(string title, string body)
    {
        TitleText.Text = title;
        BodyText.Text = body;
        if (!IsVisible) Show();
        // unten rechts auf dem Hauptbildschirm, über der Taskleiste
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is not null)
        {
            var area = screen.WorkingArea;
            var size = PixelSize.FromSize(Bounds.Size, screen.Scaling);
            Position = new PixelPoint(area.Right - size.Width - 16, area.Bottom - size.Height - 16);
        }
        hideTimer.Stop();
        hideTimer.Start();
    }
}
