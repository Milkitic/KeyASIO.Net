using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KeyAsio.Audio;
using KeyAsio.Audio.Caching;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime.Services;
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

    private CachedAudio? _cacheSound;

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

    public async Task InitializeKeyAudioAsync()
    {
        var waveFormat = _audioEngine.EngineWaveFormat;
        var (cachedAudio, result) =
            await _audioCacheManager.GetOrCreateOrEmptyFromFileAsync(_appSettings.Paths.HitsoundPath ?? "", waveFormat);
        _cacheSound = cachedAudio;
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
            _logger.LogDebug($"{hookKey} {action}");

            if (!_appSettings.Realtime.RealtimeMode)
            {
                if (_cacheSound != null)
                {
                    _audioEngine.PlayAudio(_cacheSound);
                }
                else
                {
                    _logger.LogWarning("Hitsound is null. Please check your path.");
                }

                return;
            }

            _playbackBuffer.Clear();
            _gameplaySessionManager.CurrentHitsoundSequencer.ProcessInteraction(_playbackBuffer,
                _appSettings.Input.Keys.IndexOf(hookKey), _appSettings.Input.Keys.Count);
            foreach (var playbackInfo in _playbackBuffer)
            {
                _sfxPlaybackService.DispatchPlayback(playbackInfo);
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
