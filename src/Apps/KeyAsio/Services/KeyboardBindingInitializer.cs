using System.ComponentModel;
using Coosu.Beatmap.Sections.GamePlay;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.OsuAudio.Hitsounds;
using KeyAsio.Core.OsuAudio.Hitsounds.Playback;
using KeyAsio.Core.OsuAudio.Utils;
using KeyAsio.Configuration;
using KeyAsio.Application.Models;
using KeyAsio.Application.Services;
using KeyAsio.Sync.Models;
using KeyAsio.Sync.AudioProviders;
using KeyAsio.Sync.Services;
using KeyAsio.Common;
using Microsoft.Extensions.Logging;
using Milki.Extensions.MouseKeyHook;
using NAudio.Wave;

namespace KeyAsio.Services;

public class KeyboardBindingInitializer
{
    private static readonly HookModifierKeys[] s_modifiers =
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
    private readonly IPlaybackEngine _playbackEngine;
    private readonly GameplaySessionManager _gameplaySessionManager;
    private readonly SfxPlaybackService _sfxPlaybackService;
    private readonly SkinManager _skinManager;
    private readonly ApplicationState _sharedViewModel;

    private IKeyboardHook _keyboardHook = null!;
    public IKeyboardHook KeyboardHook => _keyboardHook;
    private readonly List<Guid> _registerList = new();
    private readonly List<PlaybackInfo> _playbackBuffer = new(64);
    private readonly OsuAudioFileCache _osuAudioFileCache = new();

    private CachedAudio? _cachedKeyOnlyAudio;
    private string? _cachedKeyOnlyAudioKey;

    public KeyboardBindingInitializer(
        ILogger<KeyboardBindingInitializer> logger,
        AppSettings appSettings,
        AudioCacheManager audioCacheManager,
        IPlaybackEngine playbackEngine,
        GameplaySessionManager gameplaySessionManager,
        SfxPlaybackService sfxPlaybackService,
        SkinManager skinManager,
        ApplicationState sharedViewModel)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioCacheManager = audioCacheManager;
        _playbackEngine = playbackEngine;
        _gameplaySessionManager = gameplaySessionManager;
        _sfxPlaybackService = sfxPlaybackService;
        _skinManager = skinManager;
        _sharedViewModel = sharedViewModel;
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
                    // IsAudioPaused reflects interpolation freeze protection as well as an actual pause.
                    // Stable memory timing can enter that state briefly during normal gameplay, so it must
                    // not be used to discard a physical key press.
                    sequencer.ProcessInteraction(_playbackBuffer, keyIndex, keyTotal);
                    foreach (var playbackInfo in _playbackBuffer)
                    {
                        _sfxPlaybackService.DispatchPlayback(playbackInfo);
                    }
                }
            }
            else
            {
                if (_playbackEngine.CurrentDevice is null)
                {
                    _logger.LogWarning("Engine not ready.");
                    return;
                }

                var cachedAudio = ResolveKeyOnlyAudio();
                _sfxPlaybackService.PlayEffectsAudio(cachedAudio, 1, 0);
            }
        };

        foreach (var modifier in s_modifiers)
        {
            _registerList.Add(modifier == HookModifierKeys.None
                ? _keyboardHook.RegisterKey(key, callback)
                : _keyboardHook.RegisterHotkey(modifier, key, callback));
        }
    }

    private CachedAudio ResolveKeyOnlyAudio()
    {
        var waveFormat = _playbackEngine.EngineWaveFormat;
        const string sampleName = "soft-hitnormal";

        string? cacheKey = null;
        CachedAudio? cachedAudio = null;

        var selectedSkin = _sharedViewModel.SelectedSkin;
        var selectedSkinName = _appSettings.Paths.SelectedSkinName;
        var osuFolder = _appSettings.Paths.OsuFolderPath;
        var skinFolder = selectedSkin?.Folder ?? "";

        if (selectedSkin != null &&
            !string.Equals(selectedSkin.FolderName, SkinDescription.Internal.FolderName, StringComparison.OrdinalIgnoreCase))
        {
            // Lazer built-in skins (argon, triangles, classic, retro)
            if (skinFolder.StartsWith("{lazer-", StringComparison.OrdinalIgnoreCase))
            {
                cachedAudio = TryLoadLazerBuiltinAudio(skinFolder, sampleName, waveFormat, out cacheKey)
                              ?? TryLoadLazerBuiltinAudio(skinFolder, "normal-hitnormal", waveFormat, out cacheKey);
            }
            // Stable classic skin
            else if (string.Equals(selectedSkin.FolderName, SkinDescription.Classic.FolderName, StringComparison.OrdinalIgnoreCase))
            {
                cachedAudio = TryLoadClassicAudio(waveFormat, out cacheKey);
            }
            // Stable custom skin
            else if (!string.IsNullOrWhiteSpace(osuFolder))
            {
                var stableSkinFolder = Path.Combine(osuFolder, "Skins", selectedSkinName);
                if (Directory.Exists(stableSkinFolder))
                {
                    cachedAudio = TryLoadSkinAudio(stableSkinFolder, sampleName, waveFormat, out cacheKey)
                                  ?? TryLoadSkinAudio(stableSkinFolder, "normal-hitnormal", waveFormat, out cacheKey);
                }
            }

            // Lazer custom skins: try catalog
            if (cachedAudio == null && _skinManager.TryGetSkinCatalog(skinFolder, out var catalog))
            {
                cachedAudio = TryLoadSkinCatalogAudio(catalog, sampleName, waveFormat, out cacheKey)
                              ?? TryLoadSkinCatalogAudio(catalog, "normal-hitnormal", waveFormat, out cacheKey);
            }

            // Fallback: lazer classic sounds (lazer mode) or stable classic (stable mode)
            if (cachedAudio == null)
            {
                cachedAudio = TryLoadLazerBuiltinAudio("{lazer-classic}", sampleName, waveFormat, out cacheKey)
                              ?? TryLoadLazerBuiltinAudio("{lazer-classic}", "normal-hitnormal", waveFormat, out cacheKey)
                              ?? TryLoadClassicAudio(waveFormat, out cacheKey);
            }
        }

        if (cachedAudio == null)
        {
            cacheKey = $"internal://dynamic/{sampleName}";
            cachedAudio = _audioCacheManager.CreateDynamic(cacheKey, waveFormat);
        }

        if (_cachedKeyOnlyAudio == null || _cachedKeyOnlyAudioKey != cacheKey)
        {
            _cachedKeyOnlyAudio = cachedAudio;
            _cachedKeyOnlyAudioKey = cacheKey;
        }

        return _cachedKeyOnlyAudio;
    }

    private CachedAudio? TryLoadClassicAudio(WaveFormat waveFormat, out string? cacheKey)
    {
        cacheKey = null;
        string resourceName = "soft-hitnormal";

        if (!_skinManager.TryGetStableResource(resourceName, out var data))
        {
            resourceName = "normal-hitnormal";
            if (!_skinManager.TryGetStableResource(resourceName, out data))
            {
                return null;
            }
        }

        cacheKey = $"classic://{resourceName}";
        using var stream = new MemoryStream(data);
        var result = _audioCacheManager.GetOrCreateOrEmptyAsync(cacheKey, stream, waveFormat).GetAwaiter().GetResult();
        return result.CachedAudio;
    }

    private CachedAudio? TryLoadSkinAudio(string skinFolder, string filenameWithoutExt, WaveFormat waveFormat,
        out string? cacheKey)
    {
        cacheKey = null;
        var filename = _osuAudioFileCache.GetFileUntilFind(skinFolder, filenameWithoutExt, out var resourceOwner);
        if (resourceOwner != ResourceOwner.Beatmap)
        {
            return null;
        }

        var path = Path.Combine(skinFolder, filename);
        var result = _audioCacheManager.GetOrCreateOrEmptyFromFileAsync(path, waveFormat).GetAwaiter().GetResult();
        cacheKey = path;
        return result.CachedAudio;
    }

    private CachedAudio? TryLoadSkinCatalogAudio(IBeatmapResourceCatalog catalog, string filenameWithoutExt,
        WaveFormat waveFormat, out string? cacheKey)
    {
        cacheKey = null;
        if (!catalog.TryResolveAudio(filenameWithoutExt, out var resource))
        {
            return null;
        }

        var result = _audioCacheManager.GetOrCreateOrEmptyFromFileAsync(resource.Path, waveFormat).GetAwaiter().GetResult();
        cacheKey = resource.Path;
        return result.CachedAudio;
    }

    private CachedAudio? TryLoadLazerBuiltinAudio(string skinFolder, string filenameWithoutExt,
        WaveFormat waveFormat, out string? cacheKey)
    {
        cacheKey = null;
        if (!_skinManager.TryGetLazerResource(skinFolder, filenameWithoutExt, out var data))
        {
            return null;
        }

        cacheKey = $"lazer://{skinFolder}/{filenameWithoutExt}";
        using var stream = new MemoryStream(data);
        var result = _audioCacheManager.GetOrCreateOrEmptyAsync(cacheKey, stream, waveFormat).GetAwaiter().GetResult();
        return result.CachedAudio;
    }
}
