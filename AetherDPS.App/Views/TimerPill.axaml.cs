using Avalonia.Controls;

namespace AetherDPS.App.Views;

public partial class TimerPill : UserControl
{
    public const double PillWidth = 150, PillHeight = 46, Gap = 6;

    public TimerPill() => InitializeComponent();

    /// <summary>Timer-Name (Schlüssel in der Gruppe).</summary>
    public string Key { get; init; } = "";
}
