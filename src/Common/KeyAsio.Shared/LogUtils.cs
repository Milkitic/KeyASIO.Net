using System.Runtime.CompilerServices;
using KeyAsio.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;

namespace KeyAsio.Shared;

public static class LogUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogToSentry(LogLevel logLevel, string content, Exception? exception = null,
        Action<Scope>? configureScope = null)
    {
        var settings = ConfigurationFactory.GetConfiguration<AppSettings>(
            ".", "appsettings.yaml", MyYamlConfigurationConverter.Instance);
        if (settings.Logging?.EnableErrorReporting != true) return;
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
            //scope.SetTag("osu.filename_real", RealtimeModeManager.Instance.OsuFile?.ToString() ?? "");
            //scope.SetTag("osu.status", RealtimeModeManager.Instance.OsuStatus.ToString());
            //var username = RealtimeModeManager.Instance.Username;
            //scope.SetTag("osu.username", string.IsNullOrEmpty(username)
            //    ? EncodeUtils.FromBase64StringEmptyIfError(settings.PlayerBase64, Encoding.ASCII)
            //    : username);

            // todo: BREAK: realtime dep
            configureScope?.Invoke(scope);
        }
    }
}