using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace KeyAsio.Utils;

using Avalonia;

internal static class AppExtensions
{
    public static IClassicDesktopStyleApplicationLifetime? CurrentDesktop =>
        Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
}