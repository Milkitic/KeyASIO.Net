using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using ILogger = KeyAsio.MemoryReading.Logging.ILogger;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace KeyAsio.Shared;

internal class WrapperLoggerFactory : MemoryReading.Logging.ILoggerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public WrapperLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public ILogger CreateLogger(string name) => new WrapperLogger(_loggerFactory.CreateLogger(name));
    public MemoryReading.Logging.ILogger<T> CreateLogger<T>() => new WrapperLogger<T>(_loggerFactory.CreateLogger<T>());

    public static WrapperLoggerFactory CreateFromExtensions() =>
        new(LoggerFactory.Create(k => k.AddNLog().SetMinimumLevel(LogLevel.Trace)));
}