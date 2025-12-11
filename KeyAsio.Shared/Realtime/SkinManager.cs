using System.Diagnostics;
using KeyAsio.Audio.Caching;
using KeyAsio.MemoryReading;
using KeyAsio.Shared.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Milki.Extensions.Configuration;
using KeyAsio.MemoryReading.OsuMemoryModels;

namespace KeyAsio.Shared.Realtime;

public class SkinManager : IHostedService
{
    private static readonly Lock InstanceLock = new();
    private readonly ILogger<SkinManager> _logger;
    private readonly AppSettings _appSettings;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly MemoryScan _memoryScan;
    private readonly SharedViewModel _sharedViewModel;
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;
    private bool _waiting;

    public SkinManager(ILogger<SkinManager> logger, AppSettings appSettings, AudioCacheManager audioCacheManager,
        MemoryScan memoryScan, SharedViewModel sharedViewModel)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioCacheManager = audioCacheManager;
        _memoryScan = memoryScan;
        _sharedViewModel = sharedViewModel;
    }

    public void ListenPropertyChanging()
    {
        _sharedViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_sharedViewModel.SelectedSkin))
            {
                _appSettings.Paths.SelectedSkinName = _sharedViewModel.SelectedSkin?.FolderName ?? "";
                _audioCacheManager.Clear("internal");
            }
        };

        _appSettings.Paths.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.Paths.OsuFolderPath))
            {
                _ = RefreshSkinInBackground();
            }
        };
    }

    public void ListenToProcess()
    {
        var memoryReadObject = _memoryScan.MemoryReadObject;
        memoryReadObject.OsuStatusChanged += (pre, current) =>
        {
            if (current is OsuMemoryStatus.NotRunning or OsuMemoryStatus.Unknown)
            {
                return;
            }

            if (pre is not (OsuMemoryStatus.NotRunning or OsuMemoryStatus.Unknown))
            {
                return;
            }

            var process = Process.GetProcessesByName("osu!");
            foreach (var proc in process)
            {
                var mainModule = proc.MainModule?.FileName;
                if (mainModule == null) continue;
                var fileVersionInfo = FileVersionInfo.GetVersionInfo(mainModule);
                if (fileVersionInfo.CompanyName != "ppy") continue;
                _appSettings.Paths.OsuFolderPath = Path.GetDirectoryName(Path.GetFullPath(mainModule));
                break;
            }
        };
    }

    public async Task RefreshSkinInBackground()
    {
        lock (InstanceLock)
        {
            if (_waiting) return;
            _waiting = true;
        }

        await StopRefreshTask();
        _cts = new CancellationTokenSource();
        _refreshTask = new Task(() =>
        {
            if (_appSettings.Paths.OsuFolderPath == null) return;
            var skinsDir = Path.Combine(_appSettings.Paths.OsuFolderPath, "Skins");
            if (!Directory.Exists(skinsDir)) return;
            UiDispatcher.Invoke(() => _sharedViewModel.Skins.Clear());
            var list = new List<SkinDescription> { SkinDescription.Default };
            foreach (var directory in Directory.EnumerateDirectories(skinsDir, "*", SearchOption.TopDirectoryOnly))
            {
                if (_cts.IsCancellationRequested) return;
                string? name = null;
                string? author = null;
                var iniFile = Path.Combine(directory, "skin.ini");
                if (File.Exists(iniFile))
                {
                    (name, author) = ReadIniFile(iniFile);
                }

                var skinDescription = new SkinDescription(Path.GetFileName(directory), directory, name, author);
                list.Add(skinDescription);
                _logger.LogDebug("Find skin: {SkinDescription}", skinDescription);
                if (_cts.IsCancellationRequested) return;
            }

            UiDispatcher.Invoke(() =>
            {
                foreach (var skinDescription in list)
                {
                    _sharedViewModel.Skins.Add(skinDescription);
                }

                var selected = _appSettings.Paths.SelectedSkinName;
                _sharedViewModel.SelectedSkin = _sharedViewModel.Skins.FirstOrDefault(k => k.FolderName == selected) ??
                                                _sharedViewModel.Skins.FirstOrDefault();
            });
        });

        lock (InstanceLock)
        {
            _waiting = false;
        }

        _refreshTask.Start();
    }

    public async Task CheckOsuRegistryAsync()
    {
        try
        {
            using var reg = Registry.ClassesRoot.OpenSubKey(@"osu!\shell\open\command");
            var parameters = reg?.GetValue(null)?.ToString();
            if (parameters == null) return;

            var path = parameters.Replace(" \"%1\"", "").Trim(' ', '"');
            _appSettings.Paths.OsuFolderPath = Path.GetDirectoryName(Path.GetFullPath(path));
            _appSettings.Save();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurs while finding registry");
        }
    }

    private static (string?, string?) ReadIniFile(string iniFile)
    {
        string? name = null;
        string? author = null;

        using var fs = File.Open(iniFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sr = new StreamReader(fs);

        while (sr.ReadLine() is { } line)
        {
            if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
            {
                name = line.AsSpan(5).Trim().ToString();
            }
            else if (line.StartsWith("Author:", StringComparison.OrdinalIgnoreCase))
            {
                author = line.AsSpan(7).Trim().ToString();
            }

            if (name is not null && author is not null)
            {
                break;
            }
        }

        return (name, author);
    }

    private async Task StopRefreshTask()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        if (_refreshTask != null)
        {
            await _refreshTask;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_appSettings.Paths.OsuFolderPath))
        {
            await CheckOsuRegistryAsync();
        }

        ListenPropertyChanging();
        _ = RefreshSkinInBackground();
        if (_appSettings.Realtime.RealtimeMode)
        {
            ListenToProcess();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}