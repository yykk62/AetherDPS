using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AetherDPS.App.Views;

public partial class PetsView : UserControl
{
    public PetsView() => InitializeComponent();

    private void OnCenter(object? sender, RoutedEventArgs e) => FinderMap.CenterOnSelf();
}
