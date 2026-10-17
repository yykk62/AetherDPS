using AetherDPS.App.Services;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace AetherDPS.App.Views;

public partial class ConnectionPanel : UserControl
{
    public ConnectionPanel() => InitializeComponent();

    /// <summary>Prüfschritt in Ordnung → Haken, sonst Warnzeichen (Form zusätzlich zur Farbe).</summary>
    public static readonly IValueConverter OkIcon = new FuncValueConverter<bool, object?>(ok => Icons.Get(ok ? "check" : "warning", 14));
}
