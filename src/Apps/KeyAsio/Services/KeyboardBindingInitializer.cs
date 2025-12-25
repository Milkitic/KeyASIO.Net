using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Sync.Services;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.Services;

public class KeyboardBindingInitializer
{
    private static readonly HookModifierKeys[] Modifiers =
    [
        HookModifierKeys.None,
        HookModifierKeys.Control,
        HookModifierKeys.Shift,
        HookModifierKeys.Alt,
        HookModifierKeys.Control | HookModifierKeys.Alt,
        HookModifierKeys.Control | HookModifierKeys.Shift,
        HookModifierKeys.Shift | HookModifierKeys.Alt,
        HookModifierKeys.Control | HookModifierKeys.Shift | HookModifierKeys.Alt
    ];

    private readonly ILogger<KeyboardBindingInitializer> _logger;
    private readonly AppSettings _appSettings;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly AudioEngine _audioEngine;
    private readonly GameplaySessionManager _gameplaySessionManager;
    private readonly SfxPlaybackService _sfxPlaybackService;

    private IKeyboardHook _keyboardHook = null!;
    public IKeyboardHook KeyboardHook => _keyboardHook;
    private readonly List<Guid> _registerList = new();
    private readonly List<PlaybackInfo> _playbackBuffer = new(64);

    private CachedAudio? _cachedKeyOnlyAudio;

    public KeyboardBindingInitializer(
        ILogger<KeyboardBindingInitializer> logger,
        AppSettings appSettings,
        AudioCacheManager audioCacheManager,
        AudioEngine audioEngine,
        GameplaySessionManager gameplaySessionManager,
        SfxPlaybackService sfxPlaybackService)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioCacheManager = audioCacheManager;
        _audioEngine = audioEngine;
        _gameplaySessionManager = gameplaySessionManager;
        _sfxPlaybackService = sfxPlaybackService;
    }

    public void Setup()
    {
        _keyboardHook = _appSettings.Input.UseRawInput
            ? KeyboardHookFactory.CreateRawInput()
            : KeyboardHookFactory.CreateGlobal();
    }

    public void RegisterKeys(IEnumerable<HookKeys> keys)
    {
        foreach (var key in keys.Distinct())
        {
            try
            {
                RegisterKey(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register key {Key}", key);
            }
        }
    }

    public void UnregisterAll()
    {
        foreach (var guid in _registerList.ToList())
        {
            _keyboardHook.TryUnregister(guid);
        }

        _registerList.Clear();
    }

    private void RegisterKey(HookKeys key)
    {
        KeyboardCallback callback = (_, hookKey, action) =>
        {
            if (action != KeyAction.KeyDown) return;
            _logger.LogTrace("{HookKeys} {KeyAction}", hookKey, action);

            if (_appSettings.Sync.EnableSync)
            {
                _playbackBuffer.Clear();
                _gameplaySessionManager.CurrentHitsoundSequencer.ProcessInteraction(_playbackBuffer,
                    _appSettings.Input.Keys.IndexOf(hookKey), _appSettings.Input.Keys.Count);
                foreach (var playbackInfo in _playbackBuffer)
                {
                    _sfxPlaybackService.DispatchPlayback(playbackInfo);
                }
            }
            else
            {
                if (_audioEngine.CurrentDevice is null)
                {
                    _logger.LogWarning("Engine not ready.");
                    return;
                }

                if (_cachedKeyOnlyAudio == null)
                {
                    var cachedAudio = _audioCacheManager.CreateDynamic($"internal://dynamic/soft-hitnormal", _audioEngine.EngineWaveFormat);
                    _cachedKeyOnlyAudio = cachedAudio;
                }

                _sfxPlaybackService.PlayEffectsAudio(_cachedKeyOnlyAudio, 1, 0);
            }
        };

        foreach (var modifier in Modifiers)
        {
            _registerList.Add(modifier == HookModifierKeys.None
                ? _keyboardHook.RegisterKey(key, callback)
                : _keyboardHook.RegisterHotkey(modifier, key, callback));
        }
    }
}
