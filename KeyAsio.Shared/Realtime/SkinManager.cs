using System.Diagnostics;
using KeyAsio.Audio.Caching;
using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using Milki.Extensions.Configuration;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime;

public class SkinManager : IHostedService
{
    private static readonly Lock InstanceLock = new();
    private static readonly ILogger Logger = LogUtils.GetLogger("SkinManager");
    private readonly AppSettings _appSettings;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly SharedViewModel _sharedViewModel;
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;
    private bool _waiting;

    public SkinManager(AppSettings appSettings, AudioCacheManager audioCacheManager, SharedViewModel sharedViewModel)
    {
        _appSettings = appSettings;
        _audioCacheManager = audioCacheManager;
        _sharedViewModel = sharedViewModel;
    }

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public void ListenPropertyChanging()
    {
        _sharedViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_sharedViewModel.SelectedSkin))
            {
                AppSettings.SelectedSkin = _sharedViewModel.SelectedSkin?.FolderName ?? "";
                _audioCacheManager.Clear("internal");
            }
        };

        AppSettings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.OsuFolder))
            {
                _ = RefreshSkinInBackground();
            }
        };
    }

    public void ListenToProcess()
    {
        var memoryReadObject = MemoryScan.MemoryReadObject;
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
                AppSettings.OsuFolder = Path.GetDirectoryName(Path.GetFullPath(mainModule));
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
            if (AppSettings.OsuFolder == null) return;
            var skinsDir = Path.Combine(AppSettings.OsuFolder, "Skins");
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
                Logger.Debug("Find skin: " + skinDescription);
                if (_cts.IsCancellationRequested) return;
            }

            UiDispatcher.Invoke(() =>
            {
                foreach (var skinDescription in list)
                {
                    _sharedViewModel.Skins.Add(skinDescription);
                }

                var selected = AppSettings.SelectedSkin;
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
            var settings = AppSettings;
            using var reg = Registry.ClassesRoot.OpenSubKey(@"osu!\shell\open\command");
            var parameters = reg?.GetValue(null)?.ToString();
            if (parameters == null) return;

            var path = parameters.Replace(" \"%1\"", "").Trim(' ', '"');
            settings.OsuFolder = Path.GetDirectoryName(Path.GetFullPath(path));
            settings.Save();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurs while finding registry", true);
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
        if (string.IsNullOrWhiteSpace(_appSettings.OsuFolder))
        {
            await CheckOsuRegistryAsync();
        }

        ListenPropertyChanging();
        _ = RefreshSkinInBackground();
        if (_appSettings.RealtimeOptions.RealtimeMode)
        {
            ListenToProcess();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
