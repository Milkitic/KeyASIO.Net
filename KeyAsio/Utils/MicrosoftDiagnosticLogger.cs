using Microsoft.Extensions.Logging;
using Sentry.Extensibility;

namespace KeyAsio.Utils;

public class MicrosoftDiagnosticLogger : IDiagnosticLogger
{
    private readonly ILogger _logger;

    public MicrosoftDiagnosticLogger(ILogger logger)
    {
        _logger = logger;
    }

    public bool IsEnabled(SentryLevel level)
    {
        return _logger.IsEnabled(ToMicrosoftLogLevel(level));
    }

    public void Log(SentryLevel level, string message, Exception? exception = null, params object?[] args)
    {
        _logger.Log(ToMicrosoftLogLevel(level), exception, message, args!);
    }

    private static LogLevel ToMicrosoftLogLevel(SentryLevel level)
    {
        return level switch
        {
            SentryLevel.Debug => LogLevel.Debug,
            SentryLevel.Info => LogLevel.Information,
            SentryLevel.Warning => LogLevel.Warning,
            SentryLevel.Error => LogLevel.Error,
            SentryLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.Trace,
        };
    }
}