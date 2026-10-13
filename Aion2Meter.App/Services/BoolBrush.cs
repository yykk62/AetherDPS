using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aion2Meter.App.Services;

public static class BoolBrush
{
    public static readonly IValueConverter ConnectedGreenElseAmber = new FuncValueConverter<bool, IBrush>(
        connected => connected ? new SolidColorBrush(Color.Parse("#4CC36A")) : new SolidColorBrush(Color.Parse("#E0A84A")));
}
