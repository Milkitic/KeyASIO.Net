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
            var size = 19;
            switch (key)
            {
                // Mouse
                case HookKeys.LButton:
                    return new MaterialIcon { Kind = MaterialIconKind.MouseLeftClick, Width = size, Height = size };
                case HookKeys.RButton:
                    return new MaterialIcon { Kind = MaterialIconKind.MouseRightClick, Width = size, Height = size };
                //case HookKeys.MButton:
                //    return new MaterialIcon { Kind = MaterialIconKind.MouseMiddleClick, Width = size, Height = size };
                case HookKeys.XButton1:
                    return new MaterialIcon { Kind = MaterialIconKind.MouseMoveVertical, Width = size, Height = size }; // Approximate
                case HookKeys.XButton2:
                    return new MaterialIcon { Kind = MaterialIconKind.MouseMoveVertical, Width = size, Height = size }; // Approximate

                // Arrows
                case HookKeys.Up:
                    return new MaterialIcon { Kind = MaterialIconKind.ArrowUp, Width = size, Height = size };
                case HookKeys.Down:
                    return new MaterialIcon { Kind = MaterialIconKind.ArrowDown, Width = size, Height = size };
                case HookKeys.Left:
                    return new MaterialIcon { Kind = MaterialIconKind.ArrowLeft, Width = size, Height = size };
                case HookKeys.Right:
                    return new MaterialIcon { Kind = MaterialIconKind.ArrowRight, Width = size, Height = size };

                // Editing & Navigation
                case HookKeys.Space:
                    return new MaterialIcon { Kind = MaterialIconKind.KeyboardSpace, Width = size, Height = size };
                case HookKeys.Return: // Enter
                    return new MaterialIcon { Kind = MaterialIconKind.KeyboardReturn, Width = size, Height = size };
                case HookKeys.Back:
                    return new MaterialIcon { Kind = MaterialIconKind.KeyboardBackspace, Width = size, Height = size };
                case HookKeys.Tab:
                    return new MaterialIcon { Kind = MaterialIconKind.KeyboardTab, Width = size, Height = size };
                case HookKeys.Escape:
                    return new MaterialIcon { Kind = MaterialIconKind.KeyboardEsc, Width = size, Height = size };
                case HookKeys.Delete:
                    return new MaterialIcon { Kind = MaterialIconKind.Delete, Width = size, Height = size };
                case HookKeys.Home:
                    return new MaterialIcon { Kind = MaterialIconKind.ArrowCollapseUp, Width = size, Height = size };
                case HookKeys.End:
                    return new MaterialIcon { Kind = MaterialIconKind.ArrowCollapseDown, Width = size, Height = size };
                case HookKeys.PageUp:
                    return new MaterialIcon { Kind = MaterialIconKind.ChevronDoubleUp, Width = size, Height = size };
                case HookKeys.PageDown:
                    return new MaterialIcon { Kind = MaterialIconKind.ChevronDoubleDown, Width = size, Height = size };

                // Modifiers
                case HookKeys.LShiftKey:
                case HookKeys.RShiftKey:
                case HookKeys.ShiftKey:
                case HookKeys.Shift:
                    return new MaterialIcon { Kind = MaterialIconKind.AppleKeyboardShift, Width = size, Height = size };
                case HookKeys.LControlKey:
                case HookKeys.RControlKey:
                case HookKeys.ControlKey:
                case HookKeys.Control:
                    return new MaterialIcon { Kind = MaterialIconKind.AppleKeyboardCommand, Width = size, Height = size };
                case HookKeys.LMenu:
                case HookKeys.RMenu:
                case HookKeys.Menu:
                case HookKeys.Alt:
                    return new MaterialIcon { Kind = MaterialIconKind.AppleKeyboardOption, Width = size, Height = size };
                case HookKeys.LWin:
                case HookKeys.RWin:
                    return new MaterialIcon { Kind = MaterialIconKind.MicrosoftWindows, Width = size, Height = size };

                // Media
                case HookKeys.VolumeUp:
                    return new MaterialIcon { Kind = MaterialIconKind.VolumeHigh, Width = size, Height = size };
                case HookKeys.VolumeDown:
                    return new MaterialIcon { Kind = MaterialIconKind.VolumeMedium, Width = size, Height = size };
                case HookKeys.VolumeMute:
                    return new MaterialIcon { Kind = MaterialIconKind.VolumeOff, Width = size, Height = size };
                case HookKeys.MediaPlayPause:
                    return new MaterialIcon { Kind = MaterialIconKind.PlayPause, Width = size, Height = size };
                case HookKeys.MediaNextTrack:
                    return new MaterialIcon { Kind = MaterialIconKind.SkipNext, Width = size, Height = size };
                case HookKeys.MediaPreviousTrack:
                    return new MaterialIcon { Kind = MaterialIconKind.SkipPrevious, Width = size, Height = size };
                case HookKeys.MediaStop:
                    return new MaterialIcon { Kind = MaterialIconKind.Stop, Width = size, Height = size };

                // System
                case HookKeys.CapsLock:
                    return new MaterialIcon { Kind = MaterialIconKind.KeyboardCapslock, Width = size, Height = size };
                case HookKeys.PrintScreen:
                    return new MaterialIcon { Kind = MaterialIconKind.MonitorScreenshot, Width = size, Height = size };

                default:
                    // Shorten common names if no icon
                    return key switch
                    {
                        HookKeys.Oemtilde => "~",
                        HookKeys.OemMinus => "-",
                        HookKeys.Oemplus => "+",
                        HookKeys.OemOpenBrackets => "[",
                        HookKeys.OemCloseBrackets => "]",
                        HookKeys.OemPipe => "\\",
                        HookKeys.OemSemicolon => ";",
                        HookKeys.OemQuotes => "'",
                        HookKeys.Oemcomma => ",",
                        HookKeys.OemPeriod => ".",
                        HookKeys.OemQuestion => "/",
                        _ => key.ToString()
                    };
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
                HookKeys.Up => "↑",
                HookKeys.Down => "↓",
                HookKeys.Left => "←",
                HookKeys.Right => "→",
                HookKeys.Space => "␣",
                HookKeys.Return => "↵",
                HookKeys.Back => "⌫",
                HookKeys.Tab => "↹",
                HookKeys.Escape => "Esc",
                HookKeys.Delete => "Del",
                HookKeys.Home => "Home",
                HookKeys.End => "End",
                HookKeys.PageUp => "PgUp",
                HookKeys.PageDown => "PgDn",
                HookKeys.LShiftKey or HookKeys.RShiftKey or HookKeys.ShiftKey or HookKeys.Shift => "⇧",
                HookKeys.LControlKey or HookKeys.RControlKey or HookKeys.ControlKey or HookKeys.Control => "Ctrl",
                HookKeys.LMenu or HookKeys.RMenu or HookKeys.Menu or HookKeys.Alt => "Alt",
                HookKeys.LWin or HookKeys.RWin => "Win",
                HookKeys.Oemtilde => "~",
                HookKeys.OemMinus => "-",
                HookKeys.Oemplus => "+",
                HookKeys.OemOpenBrackets => "[",
                HookKeys.OemCloseBrackets => "]",
                HookKeys.OemPipe => "\\",
                HookKeys.OemSemicolon => ";",
                HookKeys.OemQuotes => "'",
                HookKeys.Oemcomma => ",",
                HookKeys.OemPeriod => ".",
                HookKeys.OemQuestion => "/",
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
