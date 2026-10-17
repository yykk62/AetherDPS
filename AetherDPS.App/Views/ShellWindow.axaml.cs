using AetherDPS.App.ViewModels;
using Avalonia.Controls;

namespace AetherDPS.App.Views;

public partial class ShellWindow : Window
{
    public ShellWindow() => InitializeComponent();

    public ShellWindow(ShellViewModel vm) : this()
    {
        DataContext = vm;
        vm.Refresh();
    }
}
