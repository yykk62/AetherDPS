using AetherDPS.App.ViewModels;
using Avalonia.Controls;

namespace AetherDPS.App.Views;

public partial class ReportWindow : Window
{
    public ReportWindow() => InitializeComponent();

    public ReportWindow(ReportViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.Prepare();
        EventHandler close = (_, _) => Close();
        viewModel.CloseRequested += close;
        Closed += (_, _) => viewModel.CloseRequested -= close;
    }
}
