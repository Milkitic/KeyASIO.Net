using System;
using System.Globalization;
using System.Windows.Data;

namespace KeyAsio.Gui.Converters;

internal class IntMillisecond2TimeSpanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var i = (int)value!;
        var fromMilliseconds = TimeSpan.FromMilliseconds(i).ToString(@"mm\:ss\.fff");
        return fromMilliseconds;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}