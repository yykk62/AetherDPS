using Aion2Meter.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Aion2Meter.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => InitializeComponent();

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        CloseButton.Click += (_, _) => Close();

        HotkeyBox.GotFocus += (_, _) => viewModel.BeginCapture();
        HotkeyBox.LostFocus += (_, _) => viewModel.CancelCapture();
        // Tunnel-Phase: Tasten abfangen, bevor die TextBox sie verarbeitet
        HotkeyBox.AddHandler(KeyDownEvent, (_, e) =>
        {
            e.Handled = true;
            if (e.Key == Key.Escape)
            {
                viewModel.CancelCapture();
                CloseButton.Focus();
                return;
            }
            if (viewModel.TryCapture(e.Key, e.KeyModifiers)) CloseButton.Focus();
        }, RoutingStrategies.Tunnel);

        Closed += (_, _) => viewModel.CancelCapture();
    }
}
