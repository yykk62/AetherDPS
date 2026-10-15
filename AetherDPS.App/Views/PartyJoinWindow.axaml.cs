using AetherDPS.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AetherDPS.App.Views;

/// <summary>Neue Gruppenmitglieder am rechten Bildschirmrand (ohne Fokus zu stehlen), verschwindet nach 25 s.</summary>
public partial class PartyJoinWindow : Window
{
    private readonly DispatcherTimer hideTimer = new() { Interval = TimeSpan.FromSeconds(25) };

    public PartyJoinWindow()
    {
        InitializeComponent();
        hideTimer.Tick += (_, _) => (DataContext as PartyJoinViewModel)?.Close();
        // Maus drüber: nicht wegblenden, solange man liest
        PointerEntered += (_, _) => hideTimer.Stop();
        PointerExited += (_, _) => Restart();
        SizeChanged += (_, _) => Place();
    }

    public void Attach(PartyJoinViewModel vm)
    {
        DataContext = vm;
        vm.CardsAdded += (_, _) =>
        {
            if (!IsVisible) Show();
            Place();
            Restart();
        };
        vm.Closed += (_, _) =>
        {
            hideTimer.Stop();
            Hide();
        };
    }

    private void Restart()
    {
        hideTimer.Stop();
        hideTimer.Start();
    }

    /// <summary>Rechts, etwas über der Mitte – frei von Minikarte (oben rechts) und Erinnerungen (unten rechts).</summary>
    private void Place()
    {
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null) return;
        var area = screen.WorkingArea;
        var size = PixelSize.FromSize(Bounds.Size, screen.Scaling);
        int top = Math.Max(area.Y + 16, area.Y + area.Height / 2 - size.Height / 2 - 60);
        Position = new PixelPoint(area.Right - size.Width - 16, top);
    }
}
