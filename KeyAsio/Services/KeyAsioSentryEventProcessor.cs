using System.Text;
using KeyAsio.Shared;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Realtime.Services;
using KeyAsio.Shared.Utils;
using Sentry.Extensibility;

namespace KeyAsio.Services;

public class KeyAsioSentryEventProcessor : ISentryEventProcessor
{
    private readonly RealtimeSessionContext _realtimeSessionContext;
    private readonly GameplaySessionManager _gameplaySessionManager;
    private readonly AppSettings _appSettings;

    public KeyAsioSentryEventProcessor(
        RealtimeSessionContext realtimeSessionContext,
        GameplaySessionManager gameplaySessionManager,
        AppSettings appSettings)
    {
        _realtimeSessionContext = realtimeSessionContext;
        _gameplaySessionManager = gameplaySessionManager;
        _appSettings = appSettings;
    }

    public SentryEvent? Process(SentryEvent @event)
    {
        if (!_appSettings.Logging.EnableErrorReporting)
        {
            return null;
        }

        @event.SetTag("osu.filename_real", _gameplaySessionManager.OsuFile?.ToString() ?? "");
        @event.SetTag("osu.status", _realtimeSessionContext.OsuStatus.ToString());

        var username = _realtimeSessionContext.Username;
        var finalUsername = string.IsNullOrEmpty(username)
            ? EncodeUtils.FromBase64StringEmptyIfError(_appSettings.Logging.PlayerBase64 ?? "", Encoding.ASCII)
            : username;

        @event.SetTag("osu.username", finalUsername ?? "");

        return @event;
    }
}