using System.Globalization;
using Avalonia.Data.Converters;
using KeyAsio.Lang;

namespace KeyAsio.Converters;

public class UpdateStatusConverter : IMultiValueConverter
{
    public static readonly UpdateStatusConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // values[0]: NewVersion (string?)
        // values[1]: StatusMessage (string?)
        // values[2]: Trigger (any, usually LanguageManager.SelectedLanguageItem)

        if (values.Count < 2) return null;

        var newVersion = values[0] as string;
        var statusMessage = values[1] as string;

        if (!string.IsNullOrEmpty(newVersion))
        {
            return $"{SR.Settings_UpdateAvailablePrefix}{newVersion}";
        }

        return statusMessage;
    }
}