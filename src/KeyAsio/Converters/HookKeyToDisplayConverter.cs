using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;
using Material.Icons.Avalonia;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.Converters;

public class HookKeyToDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not HookKeys key) return value;

        if (targetType == typeof(object))
        {
            // Return icon or text based on key
            switch (key)
            {
                case HookKeys.LButton:
                    return new MaterialIcon { Kind = MaterialIconKind.MouseLeftClick, Width = 24, Height = 24 };
                case HookKeys.RButton:
                    return new MaterialIcon { Kind = MaterialIconKind.MouseRightClick, Width = 24, Height = 24 };
                //case HookKeys.MButton:
                //    return new MaterialIcon { Kind = MaterialIconKind.MouseMiddleClick, Width = 24, Height = 24 };
                case HookKeys.XButton1:
                    return new MaterialIcon { Kind = MaterialIconKind.MouseMoveVertical, Width = 24, Height = 24 }; // Approximate
                case HookKeys.XButton2:
                    return new MaterialIcon { Kind = MaterialIconKind.MouseMoveVertical, Width = 24, Height = 24 }; // Approximate
                case HookKeys.Space:
                    return new MaterialIcon { Kind = MaterialIconKind.KeyboardSpace, Width = 24, Height = 24 };
                default:
                    return key.ToString();
            }
        }
        else if (targetType == typeof(string))
        {
            // Fallback for string-only binding if needed
            return key switch
            {
                HookKeys.LButton => "M1",
                HookKeys.RButton => "M2",
                HookKeys.MButton => "M3",
                HookKeys.XButton1 => "M4",
                HookKeys.XButton2 => "M5",
                HookKeys.Space => "␣",
                _ => key.ToString()
            };
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
