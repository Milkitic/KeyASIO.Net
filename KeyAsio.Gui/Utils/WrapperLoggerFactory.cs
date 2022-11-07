using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace KeyAsio.Gui.Utils;

internal class WrapperLoggerFactory : OsuRTDataProvider.ILoggerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public WrapperLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public OsuRTDataProvider.ILogger CreateLogger(string name) => new WrapperLogger(_loggerFactory.CreateLogger(name));
    public OsuRTDataProvider.ILogger<T> CreateLogger<T>() => new WrapperLogger<T>(_loggerFactory.CreateLogger<T>());

    public static WrapperLoggerFactory CreateFromExtensions() =>
        new(LoggerFactory.Create(k => k.AddNLog().SetMinimumLevel(LogLevel.Trace)));
}