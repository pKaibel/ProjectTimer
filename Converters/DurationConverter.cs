using System.Globalization;
using ProjectTimer.Formatting;

namespace ProjectTimer.Converters;

public sealed class DurationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TimeSpan duration ? DurationFormatter.Format(duration) : "0 min";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
