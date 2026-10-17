using AetherDPS.App.ViewModels;
using Avalonia.Controls;

namespace AetherDPS.App.Views;

/// <summary>Hauptfenster (docs/DESIGN.md). Schließen blendet nur aus – die App läuft im Tray weiter.</summary>
public partial class ShellWindow : Window
{
    public ShellWindow() => InitializeComponent();

    public ShellWindow(ShellViewModel vm) : this()
    {
        DataContext = vm;
        vm.Refresh();
        Opened += (_, _) => vm.Hub.StartRefreshing();
    }

    public bool AllowClose { get; set; }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            (DataContext as ShellViewModel)?.Hub.StopRefreshing();
        }
        base.OnClosing(e);
    }

    /// <summary>Fenster zeigen und zum Ort springen, der zur alten Reiter-Nummer gehört.</summary>
    public void ShowTab(int legacyTab)
    {
        if (DataContext is ShellViewModel vm)
        {
            vm.Show(legacyTab);
            vm.Hub.StartRefreshing();
        }
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }
}
