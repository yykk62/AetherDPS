using Aion2Meter.App.ViewModels;
using Avalonia.Controls;

namespace Aion2Meter.App.Views;

public partial class HubWindow : Window
{
    public HubWindow() => InitializeComponent();

    public HubWindow(HubViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Opened += (_, _) => viewModel.StartRefreshing();
    }

    /// <summary>Beim Schließen nur ausblenden – die App läuft im Tray weiter.</summary>
    public bool AllowClose { get; set; }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            (DataContext as HubViewModel)?.StopRefreshing();
        }
        base.OnClosing(e);
    }

    public void ShowTab(int tab)
    {
        if (DataContext is HubViewModel vm)
        {
            vm.SelectedTab = tab;
            vm.StartRefreshing();
        }
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }
}
