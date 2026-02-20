using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace Aiden.TrayMonitor.Converters;

public sealed partial class ContextPercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return 0d;
        }

        var match = PercentRegex().Match(text);
        if (!match.Success)
        {
            return 0d;
        }

        return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
            ? Math.Clamp(percent, 0d, 100d)
            : 0d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*%")]
    private static partial Regex PercentRegex();
}
