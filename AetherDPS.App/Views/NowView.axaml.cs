using AetherDPS.App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AetherDPS.App.Views;

public partial class NowView : UserControl
{
    public NowView() => InitializeComponent();

    /// <summary>Deine Zeile liegt auf der gedämpften Goldfläche (Token Surface.Self), andere auf der Mulde.</summary>
    public static readonly IValueConverter SelfBackground = new FuncValueConverter<bool, IBrush?>(self =>
        self && Application.Current?.TryGetResource("Surface.Self", null, out var brush) == true ? brush as IBrush : Brushes.Transparent);

    /// <summary>Boss gelegt → Haken, sonst Kreuz (Form zusätzlich zur Farbe).</summary>
    public static readonly IValueConverter KilledIcon = new FuncValueConverter<bool, object?>(killed => Icons.Get(killed ? "check" : "cross", 16));

    /// <summary>Icon-Name aus dem Set „Aether“ → Bild.</summary>
    public static readonly IValueConverter IconOf = new FuncValueConverter<string, object?>(name => name is null ? null : Icons.Get(name, 20));
}
