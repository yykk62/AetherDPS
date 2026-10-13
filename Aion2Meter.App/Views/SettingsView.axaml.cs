using Aion2Meter.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Aion2Meter.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        HotkeyBox.GotFocus += (_, _) => (DataContext as SettingsViewModel)?.BeginCapture();
        HotkeyBox.LostFocus += (_, _) => (DataContext as SettingsViewModel)?.CancelCapture();
        // Tunnel-Phase: Tasten abfangen, bevor die TextBox sie verarbeitet
        HotkeyBox.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (DataContext is not SettingsViewModel vm) return;
            e.Handled = true;
            if (e.Key == Key.Escape)
            {
                vm.CancelCapture();
                TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
                return;
            }
            if (vm.TryCapture(e.Key, e.KeyModifiers))
                TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        }, RoutingStrategies.Tunnel);

        DetachedFromVisualTree += (_, _) => (DataContext as SettingsViewModel)?.CancelCapture();
    }
}
