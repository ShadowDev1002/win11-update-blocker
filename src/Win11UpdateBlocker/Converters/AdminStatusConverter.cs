using System.Globalization;
using System.Windows.Data;

namespace Win11UpdateBlocker.Converters;

public sealed class AdminStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Aktiv" : "Nicht aktiv";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
