using AetherDPS.App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AetherDPS.App.Views;

/// <summary>Zeile oben mittig im Platzierungsmodus: was zu tun ist, Esc/Enter, „Fertig“. Nimmt nie den Fokus.</summary>
public partial class PlacementBar : Window
{
    public PlacementBar()
    {
        InitializeComponent();
        ClickThrough.NeverActivate(this);
    }

    public event EventHandler? Done;

    private void OnDone(object? sender, RoutedEventArgs e) => Done?.Invoke(this, EventArgs.Empty);

    /// <summary>Oben mittig auf dem Hauptbildschirm (dort liegt meist das Spiel).</summary>
    public void ShowAtTop()
    {
        if (!IsVisible) Show();
        if ((Screens.Primary ?? Screens.All.FirstOrDefault()) is not { } screen) return;
        var area = screen.WorkingArea;
        var size = PixelSize.FromSize(ClientSize, screen.Scaling);
        Position = new PixelPoint(area.X + (area.Width - size.Width) / 2, area.Y + (int)(16 * screen.Scaling));
    }
}
