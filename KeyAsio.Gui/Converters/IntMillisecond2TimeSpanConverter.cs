using System;
using System.Globalization;
using System.Windows.Data;
using KeyAsio.Gui.Utils;

namespace KeyAsio.Gui.Converters;

internal class IntMillisecond2TimeSpanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var i = (int)value!;
        Span<char> c = stackalloc char[10];
        var vsb = new ValueStringBuilder(c);
        if (i < 0)
        {
            vsb.Append('-');
        }

        vsb.AppendSpanFormattable(TimeSpan.FromMilliseconds(i), @"mm\:ss\.fff");
        return vsb.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}