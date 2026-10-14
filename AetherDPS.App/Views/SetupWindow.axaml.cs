using AetherDPS.App.ViewModels;
using Avalonia.Controls;

namespace AetherDPS.App.Views;

public partial class SetupWindow : Window
{
    public SetupWindow() => InitializeComponent();

    public SetupWindow(SetupViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Activated += (_, _) => viewModel.Refresh();
    }
}
