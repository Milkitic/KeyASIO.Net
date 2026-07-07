using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Coosu.Shared.IO;
using dnlib.DotNet;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Sync;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;
using Milki.Extensions.Configuration;

namespace KeyAsio.Shared.Services;

public class SkinManager
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
    // FolderName is the GUID string; Folder points to the lazer user data directory
    // (used to resolve the realm-backed file store at runtime).
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

    private readonly ILogger<SkinManager> _logger;
    private readonly AppSettings _appSettings;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly SharedViewModel _sharedViewModel;
    private readonly LazerIpcGameSyncSource? _lazerSyncSource;
    private readonly SyncSessionContext _syncSessionContext;
    private readonly GameSyncSourceCoordinator _syncSourceCoordinator;

    private readonly AsyncLock _asyncLock = new();

    private CancellationTokenSource? _processPollingCts;
    private Task? _processPollingTask;
    private CancellationTokenSource? _skinLoadCts;

    private readonly AsyncSequentialWorker _skinLoadingWorker;

    private readonly Dictionary<string, byte[]> _dictionary = new();

    // Lazer skin context (received via IPC).
    private LazerIpcSkinInfo[]? _lazerSkinInfos;
    private string? _lazerUserDataDirectory;
    private string? _lazerExeDirectory;
    private GameClientType _lastKnownClientType = GameClientType.Stable;

    public SkinManager(ILogger<SkinManager> logger, AppSettings appSettings, AudioCacheManager audioCacheManager,
        SharedViewModel sharedViewModel, LazerIpcGameSyncSource? lazerSyncSource, SyncSessionContext syncSessionContext,
        GameSyncSourceCoordinator syncSourceCoordinator)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioCacheManager = audioCacheManager;
        _sharedViewModel = sharedViewModel;
        _lazerSyncSource = lazerSyncSource;
        _syncSessionContext = syncSessionContext;
        _syncSourceCoordinator = syncSourceCoordinator;
        _sharedViewModel.PropertyChanged += SharedViewModel_PropertyChanged;

        _skinLoadingWorker = new AsyncSequentialWorker(_logger, "SkinManagerWorker");

        if (_lazerSyncSource != null)
        {
            _lazerSyncSource.LazerSkinContextReceived += OnLazerSkinContextReceived;
        }

        _syncSourceCoordinator.ClientTypeChanged += OnClientTypeChanged;
    }

    public bool IsStarted => _processPollingCts != null;

    public bool TryGetResource(string key, [NotNullWhen(true)] out byte[]? data)
    {
        return _dictionary.TryGetValue(key, out data);
    }

    public Task ReloadSkinsAsync() => RefreshSkinsAsync();

    public void Start()
    {
        if (string.IsNullOrWhiteSpace(_appSettings.Paths.OsuFolderPath))
        {
            CheckOsuRegistry();
        }

        ListenPropertyChanging();
        _ = RefreshSkinsAsync();

        StartProcessListener();
    }

    public void Stop()
    {
        StopProcessListener();
        _skinLoadingWorker.Dispose();
    }

    public void ListenPropertyChanging()
    {
        _sharedViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_sharedViewModel.SelectedSkin))
            {
                _appSettings.Paths.SelectedSkinName = _sharedViewModel.SelectedSkin?.FolderName ?? "";
                _audioCacheManager.ClearAll();
            }
        };

        _appSettings.Paths.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.Paths.OsuFolderPath) ||
                e.PropertyName == nameof(AppSettings.Paths.AllowAutoLoadSkins))
            {
                _ = RefreshSkinsAsync();
            }
        };
    }

    private void SharedViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SharedViewModel.SelectedSkin))
        {
            _appSettings.Paths.SelectedSkinName = _sharedViewModel.SelectedSkin?.FolderName;
        }
    }

    private void OnLazerSkinContextReceived(LazerIpcSkinInfo[]? skinInfos, string? userDataDirectory, string? exeDirectory)
    {
        bool changed = false;

        if (skinInfos != null)
        {
            _lazerSkinInfos = skinInfos;
            changed = true;
        }

        if (userDataDirectory != null)
        {
            if (_lazerUserDataDirectory != userDataDirectory)
            {
                _lazerUserDataDirectory = userDataDirectory;
                changed = true;
            }
        }

        if (exeDirectory != null)
        {
            if (_lazerExeDirectory != exeDirectory)
            {
                _lazerExeDirectory = exeDirectory;
                changed = true;
            }
        }

        if (!changed)
            return;

        _logger.LogInformation(
            "Lazer skin context updated: {SkinCount} skins, user data: {UserDataDir}, exe: {ExeDir}",
            _lazerSkinInfos?.Length ?? 0, _lazerUserDataDirectory, _lazerExeDirectory);

        EnsureLazerClientTypeAndOsuFolder();
        _ = RefreshSkinsAsync();
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
            await UiDispatcher.InvokeAsync(() =>
            {
                _sharedViewModel.Skins.Clear();
                _sharedViewModel.Skins.Add(SkinDescription.Internal);
                _sharedViewModel.SelectedSkin = SkinDescription.Internal;
                foreach (var key in _dictionary.Keys)
                {
                    _dictionary[key] = Array.Empty<byte>();
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
                _appSettings.Save();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurs while finding registry");
        }
    }

    private async Task LoadSkinsInternal(CancellationToken token)
    {
        if (string.IsNullOrEmpty(_appSettings.Paths.OsuFolderPath))
        {
            // Even without an osu folder, we can still expose lazer's built-in skins.
            if (_lazerSkinInfos != null)
            {
                await LoadLazerSkinsAsync(token);
            }

            return;
        }

        if (_appSettings.Paths.ClientType == GameClientType.Lazer)
        {
            await LoadLazerSkinsAsync(token);
            return;
        }

        ExtractDefaultResources(_appSettings.Paths.OsuFolderPath, token);

        var skinsDir = Path.Combine(_appSettings.Paths.OsuFolderPath, "Skins");
        if (!Directory.Exists(skinsDir)) return;

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
        newSkinList.AddRange(loadedSkins);

        await PublishSkinListAsync(newSkinList, token);
    }

    private async Task LoadLazerSkinsAsync(CancellationToken token)
    {
        var newSkinList = new List<SkinDescription> { SkinDescription.Internal };

        // 5 built-in skins (mirrors stable's behavior: always available).
        foreach (var (_, description) in s_lazerBuiltinSkins)
        {
            newSkinList.Add(description);
        }

        // User skins from lazer realm (via IPC).
        if (_lazerSkinInfos != null)
        {
            foreach (var info in _lazerSkinInfos)
            {
                if (info.Protected)
                    continue; // Built-in skins are added separately with stable FolderNames.

                if (token.IsCancellationRequested) return;

                var folder = Path.Combine(_lazerUserDataDirectory ?? "", "files", info.Id);
                var folderName = info.Name ?? info.Id;

                newSkinList.Add(new SkinDescription(
                    folderName,
                    folder,
                    info.Name,
                    info.Creator));
            }
        }

        await PublishSkinListAsync(newSkinList, token);
    }

    private async Task PublishSkinListAsync(List<SkinDescription> newSkinList, CancellationToken token)
    {
        var selectedName = _appSettings.Paths.SelectedSkinName;
        var targetSkin = newSkinList.FirstOrDefault(k => k.FolderName == selectedName)
                         ?? SkinDescription.Internal;

        await UiDispatcher.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested) return;
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

            _sharedViewModel.SelectedSkin = targetSkin;
        });
    }

    private void ExtractDefaultResources(string osuPath, CancellationToken token)
    {
        if (_dictionary.Count > 0) return;
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
                    _dictionary[resourcesKey] = bytes;
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
}