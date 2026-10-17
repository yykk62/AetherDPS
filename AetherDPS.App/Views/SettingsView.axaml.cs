using AetherDPS.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AetherDPS.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        WireHotkeyBox(HotkeyBox, HotkeySlot.Meter);
        WireHotkeyBox(ScreenshotBox, HotkeySlot.Screenshot);
        WireHotkeyBox(PlacementBox, HotkeySlot.Placement);
        DetachedFromVisualTree += (_, _) => (DataContext as SettingsViewModel)?.CancelCapture();
    }

    /// <summary>In das Feld klicken → nächste Tastenkombination wird übernommen.</summary>
    private void WireHotkeyBox(TextBox box, HotkeySlot slot)
    {
        box.GotFocus += (_, _) => (DataContext as SettingsViewModel)?.BeginCapture(slot);
        box.LostFocus += (_, _) => (DataContext as SettingsViewModel)?.CancelCapture();
        // Tunnel-Phase: Tasten abfangen, bevor die TextBox sie verarbeitet
        box.AddHandler(KeyDownEvent, (_, e) =>
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
    }
}
