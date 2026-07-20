using System.Diagnostics;
using Coosu.Shared.IO;
using dnlib.DotNet;
using KeyAsio.Application.Abstractions;
using KeyAsio.Application.Models;
using KeyAsio.Application.Utils;
using KeyAsio.Common;
using KeyAsio.Configuration;
using KeyAsio.Configuration.Models;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.OsuAudio.Hitsounds;
using KeyAsio.Sync;
using KeyAsio.Sync.Abstractions;
using KeyAsio.Sync.Sources;
using Microsoft.Extensions.Logging;
using OverlayAPI.LazerProtocol;

namespace KeyAsio.Application.Services;

public sealed class SkinManager : ISkinResourceProvider, IDisposable
{
    private static readonly HashSet<string> s_resourcesKeys =
    [
        "taiko-normal-hitclap", "taiko-normal-hitfinish", "taiko-normal-hitnormal", "taiko-normal-hitwhistle",
        "taiko-soft-hitclap", "taiko-soft-hitfinish", "taiko-soft-hitnormal", "taiko-soft-hitwhistle",

        "drum-hitclap", "drum-hitfinish", "drum-hitnormal", "drum-hitwhistle",
        "drum-sliderslide", "drum-slidertick", "drum-sliderwhistle",

        "normal-hitclap", "normal-hitfinish", "normal-hitnormal", "normal-hitwhistle",
        "normal-sliderslide", "normal-slidertick", "normal-sliderwhistle",

        "soft-sliderslide", "soft-slidertick", "soft-sliderwhistle",
        "soft-hitclap", "soft-hitfinish", "soft-hitnormal", "soft-hitwhistle",

        "combobreak",
        "nightcore-clap", "nightcore-finish", "nightcore-hat", "nightcore-kick"
    ];

    // Lazer built-in skin folders (mirroring osu.Game SkinInfo well-known GUIDs).
    // Folder uses a synthetic identifier; actual audio files are resolved through the IPC resource catalog.
    private static readonly (string Guid, SkinDescription Description)[] s_lazerBuiltinSkins =
    [
        ("CFFA69DE-B3E3-4DEE-8563-3C4F425C05D0",
            new SkinDescription("argon", "{lazer-argon}", "osu! \"argon\" (2022)", "team osu!")),
        ("9FC9CF5D-0F16-4C71-8256-98868321AC43",
            new SkinDescription("argon_pro", "{lazer-argon_pro}", "osu! \"argon\" pro (2022)", "team osu!")),
        ("2991CFD8-2140-469A-BCB9-2EC23FBCE4AD",
            new SkinDescription("triangles", "{lazer-triangles}", "osu! \"triangles\" (2017)", "team osu!")),
        ("81F02CD3-EEC6-4865-AC23-FAE26A386187",
            new SkinDescription("classic", "{lazer-classic}", "osu! \"classic\" (2013)", "team osu!")),
        ("0555C76A-CC6B-4BB4-9548-DF76BA72EF25",
            new SkinDescription("retro", "{lazer-retro}", "osu! \"retro\" (2008)", "team osu!")),
    ];

    // Maps lazer built-in skin Folder → resource path prefix in osu.Game.Resources.dll.
    // Retro has no gameplay audio samples; it shares Legacy (classic) sounds.
    private static readonly Dictionary<string, string> s_lazerBuiltinResourcePrefixes = new()
    {
        ["{lazer-argon}"] = "Samples.Gameplay.Argon",
        ["{lazer-argon_pro}"] = "Samples.Gameplay.ArgonPro",
        ["{lazer-triangles}"] = "Samples.Gameplay",
        ["{lazer-classic}"] = "Skins.Legacy",
        ["{lazer-retro}"] = "Skins.Legacy",
    };

    private readonly ILogger<SkinManager> _logger;
    private readonly AppSettings _appSettings;
    private readonly IAppSettingsPersistence _settingsPersistence;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly IApplicationDispatcher _dispatcher;
    private readonly ApplicationState _sharedViewModel;
    private readonly LazerIpcGameSyncSource? _lazerSyncSource;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly GameSyncSourceCoordinator _syncSourceCoordinator;
    private readonly SkinSelectionPreferences _skinSelectionPreferences;
    private readonly SkinListCache _skinListCache;

    private readonly AsyncLock _asyncLock = new();

    private CancellationTokenSource? _processPollingCts;
    private Task? _processPollingTask;
    private CancellationTokenSource? _skinLoadCts;

    private readonly AsyncSequentialWorker _skinLoadingWorker;
    private bool _disposed;

    private readonly Dictionary<string, byte[]> _stableDefaultResources = new();
    private readonly Dictionary<string, IBeatmapResourceCatalog> _lazerSkinCatalogs = new();
    private readonly Dictionary<string, byte[]> _lazerDefaultResources = new();

    // Lazer skin context (received via IPC).
    private LazerSkinInfo[]? _lazerSkinInfos;
    private string? _lazerExeDirectory;
    private GameClientType _lastKnownClientType = GameClientType.Stable;

    public SkinManager(IApplicationDispatcher dispatcher,
        IAppSettingsPersistence settingsPersistence,
        ILogger<SkinManager> logger,
        AppSettings appSettings,
        AudioCacheManager audioCacheManager,
        ApplicationState sharedViewModel,
        GameSyncSourceCoordinator syncSourceCoordinator,
        LazerIpcGameSyncSource? lazerSyncSource,
        SyncSessionContext syncSessionContext)
    {
        _logger = logger;
        _appSettings = appSettings;
        _settingsPersistence = settingsPersistence;
        _audioCacheManager = audioCacheManager;
        _dispatcher = dispatcher;
        _sharedViewModel = sharedViewModel;
        _lazerSyncSource = lazerSyncSource;
        _syncSessionContext = syncSessionContext;
        _syncSourceCoordinator = syncSourceCoordinator;
        _skinSelectionPreferences = new SkinSelectionPreferences(appSettings.Paths);
        _skinListCache = new SkinListCache(logger);
        _sharedViewModel.PropertyChanged += ApplicationState_PropertyChanged;
        _appSettings.Paths.PropertyChanged += Paths_PropertyChanged;

        _skinLoadingWorker = new AsyncSequentialWorker(_logger, "SkinManagerWorker");

        if (_lazerSyncSource != null)
        {
            _lazerSyncSource.LazerSkinContextReceived += OnLazerSkinContextReceived;
        }

        _syncSourceCoordinator.ClientTypeChanged += OnClientTypeChanged;
    }

    public bool IsStarted => _processPollingCts != null;

    public bool TryGetStableResource(string key, out byte[] data)
    {
        return _stableDefaultResources.TryGetValue(key, out data);
    }

    public bool TryGetSkinCatalog(string folder, out IBeatmapResourceCatalog catalog)
    {
        return _lazerSkinCatalogs.TryGetValue(folder, out catalog);
    }

    public bool TryGetLazerResource(string skinFolder, string key, out byte[] data)
    {
        // Try the specified skin first
        if (_lazerDefaultResources.TryGetValue($"{skinFolder}:{key}", out data))
            return true;

        // Fallback: classic → triangles (for missing keys like nightcore in argon)
        if (_lazerDefaultResources.TryGetValue($"{{lazer-classic}}:{key}", out data))
            return true;

        if (_lazerDefaultResources.TryGetValue($"{{lazer-triangles}}:{key}", out data))
            return true;

        return false;
    }

    public Task ReloadSkinsAsync() => RefreshSkinsAsync();

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsStarted)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_appSettings.Paths.OsuFolderPath))
        {
            CheckOsuRegistry();
        }

        // Correct a stale persisted ClientType from a previous lazer session.
        // The coordinator starts fresh with Stable as the default active source,
        // so _syncSessionContext.ClientType reflects the actual current state.
        var liveClientType = _syncSessionContext.ClientType;
        if (_appSettings.Paths.ClientType != liveClientType)
        {
            _logger.LogInformation(
                "Correcting stale persisted ClientType {Old} -> {New}",
                _appSettings.Paths.ClientType, liveClientType);
            _appSettings.Paths.ClientType = liveClientType;
        }
        _lastKnownClientType = liveClientType;

        _ = RefreshSkinsAsync();

        StartProcessListener();
    }

    public void Stop()
    {
        StopProcessListener();
        StopRefreshTask();
    }

    private void Paths_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.Paths.OsuFolderPath) ||
            e.PropertyName == nameof(AppSettings.Paths.AllowAutoLoadSkins))
        {
            _ = RefreshSkinsAsync();
        }
    }

    private void ApplicationState_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ApplicationState.SelectedSkin))
        {
            _skinSelectionPreferences.OnSelectionChanged(
                _syncSessionContext.ClientType,
                _sharedViewModel.SelectedSkin);
            _audioCacheManager.ClearAll();
        }
    }

    private void OnLazerSkinContextReceived(LazerSkinInfo[]? skinInfos)
    {
        bool changed = false;

        if (skinInfos != null)
        {
            _lazerSkinInfos = skinInfos;
            changed = true;
        }

        var exeDirectory = FindLazerExeDirectory();
        if (exeDirectory != null && _lazerExeDirectory != exeDirectory)
        {
            _lazerExeDirectory = exeDirectory;
            changed = true;
        }

        if (!changed)
            return;

        _logger.LogInformation(
            "Lazer skin context updated: {SkinCount} skins, exe: {ExeDir}",
            _lazerSkinInfos?.Length ?? 0, _lazerExeDirectory);

        EnsureLazerClientTypeAndOsuFolder();
        _ = RefreshSkinsAsync();
    }

    private string? FindLazerExeDirectory()
    {
        var processId = _syncSessionContext.ProcessId;
        if (processId > 0)
        {
            try
            {
                var exactMatch =
                    OsuLocator.FindLazerExeDirectoryFromRunningProcess([Process.GetProcessById(processId)]);
                if (exactMatch != null)
                    return exactMatch;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // The process exited between receiving the IPC frame and resolving its executable.
            }
        }

        return OsuLocator.FindLazerExeDirectoryFromRunningProcess();
    }

    private void EnsureLazerClientTypeAndOsuFolder()
    {
        // Update ClientType to Lazer and set OsuFolderPath to lazer's exe directory.
        if (_lazerExeDirectory == null)
            return;

        _appSettings.Paths.ClientType = GameClientType.Lazer;
        _lastKnownClientType = GameClientType.Lazer;

        if (!string.Equals(_appSettings.Paths.OsuFolderPath, _lazerExeDirectory, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Updating osu folder to lazer exe directory: {Path}", _lazerExeDirectory);
            _appSettings.Paths.OsuFolderPath = _lazerExeDirectory;
        }
    }

    private void OnClientTypeChanged(GameClientType newClientType)
    {
        if (newClientType == _lastKnownClientType)
            return;

        _lastKnownClientType = newClientType;
        _appSettings.Paths.ClientType = newClientType;
        _logger.LogInformation("Sync client type changed to {ClientType}", newClientType);

        if (newClientType == GameClientType.Lazer)
        {
            _lazerExeDirectory = FindLazerExeDirectory() ?? _lazerExeDirectory;
            EnsureLazerClientTypeAndOsuFolder();
        }

        // Clear default resources so they get re-extracted from the appropriate source
        // (osu!gameplay.dll for stable, osu.Game.Resources.dll for lazer).
        _stableDefaultResources.Clear();
        _lazerSkinCatalogs.Clear();
        _lazerDefaultResources.Clear();

        if (newClientType == GameClientType.Stable)
        {
            // When switching back to stable, re-detect stable's osu folder from running process.
            CheckAndSetOsuPath(Process.GetProcessesByName("osu!"));
        }

        _ = RefreshSkinsAsync();
    }

    private void StartProcessListener()
    {
        var processes = Process.GetProcessesByName("osu!");
        CheckAndSetOsuPath(processes);

        try
        {
            _processPollingCts = new CancellationTokenSource();
            var token = _processPollingCts.Token;
            _processPollingTask = Task.Run(() => ProcessPollingLoop(token), token);
            _logger.LogInformation("Osu process listener started.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process polling.");
        }
    }

    private void StopProcessListener()
    {
        try
        {
            _processPollingCts?.Cancel();
            try
            {
                _processPollingTask?.Wait(1000);
            }
            catch (AggregateException)
            {
            }

            _processPollingCts?.Dispose();
            _processPollingCts = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping process polling.");
        }
    }

    private async Task ProcessPollingLoop(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        bool wasRunning = IsOsuRunning();

        while (await timer.WaitForNextTickAsync(token))
        {
            var processes = Process.GetProcessesByName("osu!");
            bool isRunning = processes.Length > 0;

            if (isRunning && !wasRunning)
            {
                _logger.LogInformation("Detected osu! process start via polling.");
                CheckAndSetOsuPath(processes);
                _ = RefreshSkinsAsync();
            }

            wasRunning = isRunning;
        }
    }

    private static bool IsOsuRunning()
    {
        var processes = Process.GetProcessesByName("osu!");
        bool any = processes.Length > 0;
        foreach (var p in processes) p.Dispose();
        return any;
    }

    private void CheckAndSetOsuPath(Process[] processes)
    {
        try
        {
            var detectedPath = OsuLocator.FindFromRunningProcess(processes);
            if (detectedPath != null && _appSettings.Paths.OsuFolderPath != detectedPath)
            {
                _logger.LogInformation("Auto-detected osu! path: {Path}", detectedPath);
                _appSettings.Paths.OsuFolderPath = detectedPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error inspecting osu! process module.");
        }
    }

    private async Task RefreshSkinsAsync()
    {
        using var @lock = await _asyncLock.LockAsync();

        StopRefreshTask();

        if (_appSettings.Paths.AllowAutoLoadSkins != true)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                _sharedViewModel.Skins.Clear();
                _sharedViewModel.Skins.Add(SkinDescription.Internal);
                _skinSelectionPreferences.ApplyProgrammaticSelection(
                    () => _sharedViewModel.SelectedSkin = SkinDescription.Internal);
                foreach (var key in _stableDefaultResources.Keys)
                {
                    _stableDefaultResources[key] = Array.Empty<byte>();
                }
            });
            return;
        }

        _skinLoadCts = new CancellationTokenSource();
        var token = _skinLoadCts.Token;

        _skinLoadingWorker.Enqueue(async () => await LoadSkinsInternal(token));
    }

    private void CheckOsuRegistry()
    {
        try
        {
            if (OsuLocator.FindFromRegistry() is { } path)
            {
                _appSettings.Paths.OsuFolderPath = path;
                _settingsPersistence.Save();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurs while finding registry");
        }
    }

    private async Task LoadSkinsInternal(CancellationToken token)
    {
        var clientType = _syncSessionContext.ClientType;
        var cachedSkins = _skinListCache.Get(clientType);

        if (cachedSkins.Count > 0)
        {
            await PublishSkinListAsync(cachedSkins.ToList(), clientType, token);
        }

        if (token.IsCancellationRequested || _syncSessionContext.ClientType != clientType)
        {
            return;
        }

        if (string.IsNullOrEmpty(_appSettings.Paths.OsuFolderPath))
        {
            // Even without an osu folder, we can still expose lazer's built-in skins.
            if (clientType == GameClientType.Lazer)
            {
                await LoadLazerSkinsAsync(cachedSkins, token);
            }
            else if (cachedSkins.Count == 0)
            {
                await PublishSkinListAsync(
                    [SkinDescription.Internal],
                    GameClientType.Stable,
                    token);
            }

            return;
        }

        if (clientType == GameClientType.Lazer)
        {
            await LoadLazerSkinsAsync(cachedSkins, token);
            return;
        }

        ExtractDefaultResources(_appSettings.Paths.OsuFolderPath, token);

        var skinsDir = Path.Combine(_appSettings.Paths.OsuFolderPath, "Skins");
        if (!Directory.Exists(skinsDir))
        {
            if (cachedSkins.Count == 0)
            {
                await PublishSkinListAsync(
                    [SkinDescription.Internal, SkinDescription.Classic],
                    GameClientType.Stable,
                    token);
            }

            return;
        }

        var directories = Directory.EnumerateDirectories(skinsDir);
        var loadedSkins = new List<SkinDescription>();

        foreach (var dir in directories)
        {
            if (token.IsCancellationRequested) return;
            var iniPath = Path.Combine(dir, "skin.ini");
            string? name = null;
            string? author = null;
            if (File.Exists(iniPath))
            {
                (name, author) = ReadIniFile(iniPath);
            }

            var skinDescription = new SkinDescription(Path.GetFileName(dir), dir, name, author);
            _logger.LogDebug("Find skin: {SkinDescription}", skinDescription);
            loadedSkins.Add(skinDescription);
        }

        var newSkinList = new List<SkinDescription> { SkinDescription.Internal, SkinDescription.Classic };
        newSkinList.AddRange(OrderUserSkins(loadedSkins));

        _skinListCache.Save(GameClientType.Stable, newSkinList);
        await PublishSkinListAsync(newSkinList, GameClientType.Stable, token);
    }

    private async Task LoadLazerSkinsAsync(
        IReadOnlyList<SkinDescription> cachedSkins,
        CancellationToken token)
    {
        _lazerSkinCatalogs.Clear();

        var newSkinList = new List<SkinDescription> { SkinDescription.Internal };

        // 5 built-in skins (mirrors stable's behavior: always available).
        foreach (var (_, description) in s_lazerBuiltinSkins)
        {
            newSkinList.Add(description);
        }

        // Extract default gameplay audio from osu.Game.Resources.dll for built-in skins.
        ExtractLazerDefaultResources(token);

        // User skins from lazer realm (via IPC).
        var lazerUserSkins = new List<SkinDescription>();
        if (_lazerSkinInfos != null)
        {
            foreach (var info in _lazerSkinInfos)
            {
                if (token.IsCancellationRequested) return;

                var folder = $"{{lazer-skin:{info.Id}}}";
                var folderName = string.IsNullOrWhiteSpace(info.Name) ? info.Id : info.Name;

                // Build a resource catalog from the skin's files so audio can be
                // resolved by name to the actual hash-based file store paths.
                if (info.Files.Length > 0)
                {
                    var catalog = BeatmapResourceCatalog.FromMappings(
                        info.Files.Select(f => new BeatmapResource(f.Name, f.Path)),
                        rootPath: null,
                        cacheKey: $"lazer-skin:{info.Id}");
                    _lazerSkinCatalogs[folder] = catalog;
                }

                lazerUserSkins.Add(new SkinDescription(
                    folderName,
                    folder,
                    string.IsNullOrWhiteSpace(info.Name) ? null : info.Name,
                    null));
            }
        }
        else
        {
            var knownFolders = newSkinList
                .Select(static skin => skin.FolderName)
                .ToHashSet(StringComparer.Ordinal);
            lazerUserSkins.AddRange(cachedSkins.Where(skin => knownFolders.Add(skin.FolderName)));
        }

        newSkinList.AddRange(OrderUserSkins(lazerUserSkins));

        if (_lazerSkinInfos != null)
        {
            _skinListCache.Save(GameClientType.Lazer, newSkinList);
        }

        await PublishSkinListAsync(newSkinList, GameClientType.Lazer, token);
    }

    /// <summary>
    /// Sort user skins alphabetically by their display description (case-insensitive, ordinal),
    /// matching the ordering users expect from osu! stable/lazer skin dropdowns.
    /// </summary>
    private static IEnumerable<SkinDescription> OrderUserSkins(IEnumerable<SkinDescription> userSkins)
        => userSkins.OrderBy(static s => s.Description, StringComparer.OrdinalIgnoreCase);

    private async Task PublishSkinListAsync(
        List<SkinDescription> newSkinList,
        GameClientType clientType,
        CancellationToken token)
    {
        if (token.IsCancellationRequested || _syncSessionContext.ClientType != clientType)
        {
            return;
        }

        var selectedName = _skinSelectionPreferences.Get(clientType);
        var targetSkin = clientType == GameClientType.Lazer
            ? newSkinList.FirstOrDefault(k => k.Folder == selectedName)
              // Compatibility with selections saved by older versions.
              ?? newSkinList.FirstOrDefault(k => k.FolderName == selectedName)
            : newSkinList.FirstOrDefault(k => k.FolderName == selectedName);
        targetSkin ??= SkinDescription.Internal;

        await _dispatcher.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested || _syncSessionContext.ClientType != clientType)
            {
                return;
            }

            _sharedViewModel.Skins.Clear();
            var type = SynchronizationContext.Current.GetType();
            if (type.Namespace == "System.Windows.Threading")
            {
                foreach (var skinDescription in newSkinList)
                {
                    _sharedViewModel.Skins.Add(skinDescription);
                }
            }
            else
            {
                _sharedViewModel.Skins.AddRange(newSkinList);
            }

            // Falling back while an IPC skin list is still loading must not replace
            // the user's per-client preference with the internal skin.
            _skinSelectionPreferences.ApplyProgrammaticSelection(
                () => _sharedViewModel.SelectedSkin = targetSkin);
        });
    }

    private void ExtractDefaultResources(string osuPath, CancellationToken token)
    {
        if (_stableDefaultResources.Count > 0) return;
        var dllPath = Path.Combine(osuPath, "osu!gameplay.dll");
        if (!File.Exists(dllPath))
        {
            return;
        }

        try
        {
            if (token.IsCancellationRequested) return;

            using var module = ModuleDefMD.Load(dllPath);
            var resource = module.Resources.FindEmbeddedResource("osu_gameplay.ResourcesStore.resources");
            if (resource == null)
            {
                return;
            }

            using var stream = resource.CreateReader().AsStream();
            using var reader = new System.Resources.ResourceReader(stream);

            foreach (var resourcesKey in s_resourcesKeys)
            {
                try
                {
                    reader.GetResourceData(resourcesKey, out var resourceType, out var resourceData);

                    if (!resourceType.Contains("ResourceTypeCode.ByteArray")) return;
                    // [ 长度 (Int32, 4字节) ] + [ 实际数据 (N字节) ]
                    if (resourceData.Length <= 4) return;

                    var bytes = resourceData.AsSpan(4).ToArray();
                    _stableDefaultResources[resourcesKey] = bytes;
                    _logger.LogDebug("Extracted '{ResourcesKey}' ({Bytes} bytes)", resourcesKey, bytes.Length);
                }
                catch (ArgumentException)
                {
                    _logger.LogWarning("Resource '{ResourcesKey}' not found in osu!gameplay.dll", resourcesKey);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract default resources from osu!gameplay.dll");
        }
    }

    private void ExtractLazerDefaultResources(CancellationToken token)
    {
        if (_lazerDefaultResources.Count > 0) return;

        var exeDir = _lazerExeDirectory;
        if (string.IsNullOrEmpty(exeDir))
        {
            _logger.LogDebug("Lazer exe directory not available; skipping default resource extraction.");
            return;
        }

        var dllPath = Path.Combine(exeDir, "osu.Game.Resources.dll");
        if (!File.Exists(dllPath))
        {
            _logger.LogDebug("osu.Game.Resources.dll not found at {Path}", dllPath);
            return;
        }

        try
        {
            if (token.IsCancellationRequested) return;

            using var module = ModuleDefMD.Load(dllPath);
            var assemblyName = module.Assembly?.Name ?? "osu.Game.Resources";

            // Build a lookup of all embedded resource names.
            var resourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var res in module.Resources)
            {
                if (res is EmbeddedResource emb)
                    resourceNames.Add(emb.Name);
            }

            // For each built-in skin, extract its samples from the corresponding resource path.
            // e.g. argon → "osu.Game.Resources.Samples.Gameplay.Argon.normal-hitnormal.wav"
            //      classic → "osu.Game.Resources.Skins.Legacy.normal-hitnormal.wav"
            foreach (var (skinFolder, pathPrefix) in s_lazerBuiltinResourcePrefixes)
            {
                var dotPrefix = pathPrefix.Replace('/', '.');

                foreach (var key in s_resourcesKeys)
                {
                    if (token.IsCancellationRequested) return;

                    // Try .wav > .mp3 > .ogg in priority order
                    foreach (var ext in new[] { ".wav", ".mp3", ".ogg" })
                    {
                        var manifestName = $"{assemblyName}.{dotPrefix}.{key}{ext}";
                        if (!resourceNames.Contains(manifestName))
                            continue;

                        var emb = module.Resources.FindEmbeddedResource(manifestName);
                        if (emb == null) break;

                        try
                        {
                            using var stream = emb.CreateReader().AsStream();
                            using var ms = new MemoryStream();
                            stream.CopyTo(ms);
                            var storageKey = $"{skinFolder}:{key}";
                            _lazerDefaultResources[storageKey] = ms.ToArray();
                            _logger.LogDebug("Extracted '{Key}' for {Skin} from osu.Game.Resources.dll ({Bytes} bytes)",
                                key, skinFolder, _lazerDefaultResources[storageKey].Length);
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to extract '{Key}' for {Skin} from osu.Game.Resources.dll",
                                key, skinFolder);
                            break;
                        }
                    }
                }
            }

            _logger.LogInformation("Extracted {Count} lazer default audio resources from osu.Game.Resources.dll",
                _lazerDefaultResources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract default resources from osu.Game.Resources.dll");
        }
    }

    private static (string?, string?) ReadIniFile(string iniFile)
    {
        string? name = null;
        string? author = null;

        using var fs = File.Open(iniFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sr = new StreamReader(fs);

        using var lineReader = new EphemeralLineReader(sr);
        ReadOnlyMemory<char>? currentLineMemory;

        while ((currentLineMemory = lineReader.ReadLine()) != null)
        {
            var lineSpan = currentLineMemory.Value.Span;

            var commentIndex = lineSpan.IndexOf("//");
            if (commentIndex >= 0)
            {
                lineSpan = lineSpan.Slice(0, commentIndex);
            }

            var trimmedLineSpan = lineSpan.Trim();

            if (trimmedLineSpan.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
            {
                name = trimmedLineSpan.Slice(5).TrimStart().ToString();
            }
            else if (trimmedLineSpan.StartsWith("Author:", StringComparison.OrdinalIgnoreCase))
            {
                author = trimmedLineSpan.Slice(7).TrimStart().ToString();
            }

            if (name is not null && author is not null)
            {
                break;
            }
        }

        return (name, author);
    }

    private void StopRefreshTask()
    {
        if (_skinLoadCts != null)
        {
            _skinLoadCts.Cancel();
            _skinLoadCts.Dispose();
        }

        _skinLoadCts = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _sharedViewModel.PropertyChanged -= ApplicationState_PropertyChanged;
        _appSettings.Paths.PropertyChanged -= Paths_PropertyChanged;
        if (_lazerSyncSource is not null)
        {
            _lazerSyncSource.LazerSkinContextReceived -= OnLazerSkinContextReceived;
        }

        _syncSourceCoordinator.ClientTypeChanged -= OnClientTypeChanged;
        _skinLoadingWorker.Dispose();
    }
}
