using System.Text;
using KeyAsio.Shared;
using KeyAsio.Shared.Sync;
using KeyAsio.Shared.Sync.Services;
using KeyAsio.Shared.Utils;
using Sentry.Extensibility;

namespace KeyAsio.Services;

public class KeyAsioSentryEventProcessor : ISentryEventProcessor
{
    private readonly SyncSessionContext _syncSessionContext;
    private readonly GameplaySessionManager _gameplaySessionManager;
    private readonly AppSettings _appSettings;

    public KeyAsioSentryEventProcessor(
        SyncSessionContext syncSessionContext,
        GameplaySessionManager gameplaySessionManager,
        AppSettings appSettings)
    {
        _syncSessionContext = syncSessionContext;
        _gameplaySessionManager = gameplaySessionManager;
        _appSettings = appSettings;
    }

    public SentryEvent? Process(SentryEvent @event)
    {
#if DEBUG
        return null;
#endif
        if (_appSettings.Logging.EnableErrorReporting != true)
        {
            return null;
        }

        @event.SetTag("osu.filename_real", _gameplaySessionManager.OsuFile?.ToString() ?? "");
        @event.SetTag("osu.status", _syncSessionContext.OsuStatus.ToString());

        var username = _syncSessionContext.Username;
        var finalUsername = string.IsNullOrEmpty(username)
            ? EncodeUtils.FromBase64StringEmptyIfError(_appSettings.Logging.PlayerBase64 ?? "", Encoding.ASCII)
            : username;

        @event.SetTag("osu.username", finalUsername ?? "");

        return @event;
    }
}