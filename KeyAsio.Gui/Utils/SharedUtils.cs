using System;
using KeyAsio.Gui.Configuration;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace KeyAsio.Gui.Utils;

internal static class SharedUtils
{
    private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

    public static readonly ILoggerFactory LoggerFactory =
        Microsoft.Extensions.Logging.LoggerFactory.Create(k => k
            .AddNLog()
            .SetMinimumLevel(LogLevel.Trace));

    public static readonly byte[] EmptyWaveFile =
    {
        0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20,
        0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x02, 0x00, 0x44, 0xAC, 0x00, 0x00, 0x10, 0xB1, 0x02, 0x00,
        0x04, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61, 0x00, 0x00, 0x00, 0x00
    };

    public static string SizeSuffix(long value, int decimalPlaces = 1)
    {
        if (value < 0)
        {
            return "-" + SizeSuffix(-value, decimalPlaces);
        }

        int i = 0;
        double dValue = value;
        while (Math.Round(dValue, decimalPlaces) >= 1000)
        {
            dValue /= 1024;
            i++;
        }

        return string.Format("{0:n" + decimalPlaces + "} {1}", dValue, SizeSuffixes[i]);
    }

    public static ILogger GetLogger(string name)
    {
        return LoggerFactory.CreateLogger(name);
    }

    public static ILogger<T> GetLogger<T>()
    {
        return LoggerFactory.CreateLogger<T>();
    }

    public static void DebuggingLog(this ILogger logger, LogLevel logLevel, string content)
    {
        logger.Log(logLevel, "[DEBUGGING] " + content);
    }

    public static void DebuggingDebug(this ILogger logger, string content)
    {
        logger.LogDebug("[DEBUGGING] " + content);
    }

    public static void DebuggingInfo(this ILogger logger, string content)
    {
        logger.LogInformation("[DEBUGGING] " + content);
    }

    public static void DebuggingWarn(this ILogger logger, string content)
    {
        logger.LogWarning("[DEBUGGING] " + content);
    }

    public static void DebuggingError(this ILogger logger, string content)
    {
        logger.LogError("[DEBUGGING] " + content);
    }

    public static void DebuggingDebug(this ILogger logger, Exception ex, string content)
    {
        logger.LogDebug(ex, "[DEBUGGING] " + content);
    }

    public static void DebuggingInfo(this ILogger logger, Exception ex, string content)
    {
        logger.LogInformation(ex, "[DEBUGGING] " + content);
    }

    public static void DebuggingWarn(this ILogger logger, Exception ex, string content)
    {
        logger.LogWarning(ex, "[DEBUGGING] " + content);
    }

    public static void DebuggingError(this ILogger logger, Exception ex, string content)
    {
        logger.LogError(ex, "[DEBUGGING] " + content);
    }
}