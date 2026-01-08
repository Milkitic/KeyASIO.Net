using System.ComponentModel;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Sync.AudioProviders;
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
        _appSettings.Input.PropertyChanged += Input_PropertyChanged;
        RecreateKeyboardHook();
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

    public void RegisterAllKeys()
    {
        var keys = new HashSet<HookKeys>();
        if (_appSettings.Input.OsuKeys != null) keys.UnionWith(_appSettings.Input.OsuKeys);
        if (_appSettings.Input.TaikoKeys != null) keys.UnionWith(_appSettings.Input.TaikoKeys);
        if (_appSettings.Input.CatchKeys != null) keys.UnionWith(_appSettings.Input.CatchKeys);
        if (_appSettings.Input.ManiaKeys != null)
        {
            foreach (var maniaKeys in _appSettings.Input.ManiaKeys.Values)
            {
                if (maniaKeys != null) keys.UnionWith(maniaKeys);
            }
        }

        RegisterKeys(keys);
    }

    private void Input_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettingsInput.UseRawInput))
        {
            _logger.LogInformation("UseRawInput setting changed, recreating keyboard hook...");
            UnregisterAll();
            _keyboardHook?.Dispose();
            RecreateKeyboardHook();
            RegisterAllKeys();
        }
    }

    private void RecreateKeyboardHook()
    {
        var useRawInput = _appSettings.Input.UseRawInput;
        _logger.LogInformation("Initializing keyboard hook. Mode: {Mode}", useRawInput ? "RawInput" : "Global Hook");

        _keyboardHook = useRawInput
            ? KeyboardHookFactory.CreateRawInput()
            : KeyboardHookFactory.CreateGlobal();
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

                var sequencer = _gameplaySessionManager.CurrentHitsoundSequencer;
                int keyIndex = -1;
                int keyTotal = 0;

                if (sequencer is ManiaHitsoundSequencer)
                {
                    if (_gameplaySessionManager.OsuFile != null)
                    {
                        int keyCount = (int)_gameplaySessionManager.OsuFile.Difficulty.CircleSize;
                        if (_appSettings.Input.ManiaKeys.TryGetValue(keyCount, out var maniaKeys))
                        {
                            keyIndex = maniaKeys.IndexOf(hookKey);
                            keyTotal = maniaKeys.Count;
                        }
                    }
                }
                else
                {
                    var mode = _gameplaySessionManager.OsuFile?.General.Mode ?? GameMode.Circle;
                    List<HookKeys>? activeKeys = mode switch
                    {
                        GameMode.Taiko => _appSettings.Input.TaikoKeys,
                        GameMode.Catch => _appSettings.Input.CatchKeys,
                        _ => _appSettings.Input.OsuKeys
                    };

                    keyIndex = activeKeys?.IndexOf(hookKey) ?? -1;
                    keyTotal = activeKeys?.Count ?? 0;
                }

                if (keyIndex != -1)
                {
                    sequencer.ProcessInteraction(_playbackBuffer, keyIndex, keyTotal);
                    foreach (var playbackInfo in _playbackBuffer)
                    {
                        _sfxPlaybackService.DispatchPlayback(playbackInfo);
                    }
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
                    var cachedAudio = _audioCacheManager.CreateDynamic($"internal://dynamic/soft-hitnormal",
                        _audioEngine.EngineWaveFormat);
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