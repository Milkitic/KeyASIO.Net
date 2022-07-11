using System;
using System.Runtime.CompilerServices;
using System.Text;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Realtime;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Sentry;

namespace KeyAsio.Gui.Utils;

internal static class LogUtils
{
    public static readonly ILoggerFactory LoggerFactory =
        Microsoft.Extensions.Logging.LoggerFactory.Create(k => k
            .AddNLog()
            .SetMinimumLevel(LogLevel.Trace));

    public static ILogger GetLogger(string name)
    {
        return LoggerFactory.CreateLogger(name);
    }

    public static ILogger<T> GetLogger<T>()
    {
        return LoggerFactory.CreateLogger<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogToSentry(LogLevel logLevel, string content, Exception? exception = null,
        Action<Scope>? configureScope = null)
    {
        var settings = ConfigurationFactory.GetConfiguration<AppSettings>();
        if (!settings.SendLogsToDeveloper) return;
        if (exception != null)
        {
            SentrySdk.CaptureException(exception, k =>
            {
                k.SetTag("message", content);
                ConfigureScope(k);
            });
        }
        else
        {
            var sentryLevel = logLevel switch
            {
                LogLevel.Trace => SentryLevel.Debug,
                LogLevel.Debug => SentryLevel.Debug,
                LogLevel.Information => SentryLevel.Info,
                LogLevel.Warning => SentryLevel.Warning,
                LogLevel.Error => SentryLevel.Error,
                LogLevel.Critical => SentryLevel.Fatal,
                LogLevel.None => SentryLevel.Info,
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
            };
            if (exception != null)
            {
                sentryLevel = SentryLevel.Error;
                content += $"\r\n{exception}";
            }

            SentrySdk.CaptureMessage(content, ConfigureScope, sentryLevel);
        }

        void ConfigureScope(Scope scope)
        {
            scope.SetTag("osu.filename_real", RealtimeModeManager.Instance.OsuFile?.ToString() ?? "");
            scope.SetTag("osu.status", RealtimeModeManager.Instance.OsuStatus.ToString());
            var username = RealtimeModeManager.Instance.Username;
            scope.SetTag("osu.username", string.IsNullOrEmpty(username)
                ? EncodeUtils.FromBase64StringEmptyIfError(settings.PlayerBase64, Encoding.ASCII)
                : username);
            configureScope?.Invoke(scope);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DebuggingLog(this ILogger logger, LogLevel logLevel, string content, bool toSentry = false)
    {
        logger.Log(logLevel, "[DEBUGGING] " + content);
        if (toSentry) LogToSentry(logLevel, content);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DebuggingDebug(this ILogger logger, string content, bool toSentry = false)
    {
        logger.LogDebug("[DEBUGGING] " + content);
        if (toSentry) LogToSentry(LogLevel.Debug, content);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DebuggingInfo(this ILogger logger, string content, bool toSentry = false)
    {
        logger.LogInformation("[DEBUGGING] " + content);
        if (toSentry) LogToSentry(LogLevel.Information, content);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DebuggingWarn(this ILogger logger, string content, bool toSentry = false)
    {
        logger.LogWarning("[DEBUGGING] " + content);
        if (toSentry) LogToSentry(LogLevel.Warning, content);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DebuggingError(this ILogger logger, string content, bool toSentry = false)
    {
        logger.LogError("[DEBUGGING] " + content);
        if (toSentry) LogToSentry(LogLevel.Error, content);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DebuggingDebug(this ILogger logger, Exception ex, string content, bool toSentry = false)
    {
        logger.LogDebug(ex, "[DEBUGGING] " + content);
        if (toSentry) LogToSentry(LogLevel.Debug, content, ex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DebuggingInfo(this ILogger logger, Exception ex, string content, bool toSentry = false)
    {
        logger.LogInformation(ex, "[DEBUGGING] " + content);
        if (toSentry) LogToSentry(LogLevel.Information, content, ex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DebuggingWarn(this ILogger logger, Exception ex, string content, bool toSentry = false)
    {
        logger.LogWarning(ex, "[DEBUGGING] " + content);
        if (toSentry) LogToSentry(LogLevel.Warning, content, ex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DebuggingError(this ILogger logger, Exception ex, string content, bool toSentry = false)
    {
        logger.LogError(ex, "[DEBUGGING] " + content);
        if (toSentry) LogToSentry(LogLevel.Error, content, ex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Log(this ILogger logger, LogLevel logLevel, string content, bool toSentry)
    {
        logger.Log(logLevel, content);
        if (toSentry) LogToSentry(logLevel, content);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Debug(this ILogger logger, string content, bool toSentry = false)
    {
        logger.LogDebug(content);
        if (toSentry) LogToSentry(LogLevel.Debug, content);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Info(this ILogger logger, string content, bool toSentry = false)
    {
        logger.LogInformation(content);
        if (toSentry) LogToSentry(LogLevel.Information, content);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Warn(this ILogger logger, string content, bool toSentry = false)
    {
        logger.LogWarning(content);
        if (toSentry) LogToSentry(LogLevel.Warning, content);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error(this ILogger logger, string content, bool toSentry = false)
    {
        logger.LogError(content);
        if (toSentry) LogToSentry(LogLevel.Error, content);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Debug(this ILogger logger, Exception ex, string content, bool toSentry = false)
    {
        logger.LogDebug(ex, content);
        if (toSentry) LogToSentry(LogLevel.Debug, content, ex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Info(this ILogger logger, Exception ex, string content, bool toSentry = false)
    {
        logger.LogInformation(ex, content);
        if (toSentry) LogToSentry(LogLevel.Information, content, ex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Warn(this ILogger logger, Exception ex, string content, bool toSentry = false)
    {
        logger.LogWarning(ex, content);
        if (toSentry) LogToSentry(LogLevel.Warning, content, ex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error(this ILogger logger, Exception ex, string content, bool toSentry = false)
    {
        logger.LogError(ex, content);
        if (toSentry) LogToSentry(LogLevel.Error, content, ex);
    }
}