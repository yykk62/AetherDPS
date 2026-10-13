using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Aion2Meter.App.Views;

/// <summary>
/// Zeigt den letzten Screenshot im Vollbild auf einem anderen Bildschirm als dem Spiel.
/// Jeder neue Screenshot ersetzt das Bild. Klick oder ESC blendet es aus.
/// </summary>
public partial class ScreenshotWindow : Window
{
    public ScreenshotWindow()
    {
        InitializeComponent();
        PointerPressed += (_, _) => Hide();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Hide();
        };
    }

    /// <summary>Bild zeigen. Liefert false, wenn es keinen zweiten Bildschirm gibt.</summary>
    public bool ShowOnOtherScreen(Bitmap image, PixelRect gameMonitor)
    {
        var target = Screens.All.FirstOrDefault(s => !s.Bounds.Contains(gameMonitor.Center));
        if (target is null) return false;

        var old = Picture.Source as Bitmap;
        Picture.Source = image;
        old?.Dispose();

        var bounds = target.Bounds;
        Position = bounds.Position;
        Width = bounds.Width / target.Scaling;
        Height = bounds.Height / target.Scaling;
        if (!IsVisible) Show();
        Position = bounds.Position;   // nach Show erneut, falls Windows das Fenster verschoben hat
        return true;
    }
}
