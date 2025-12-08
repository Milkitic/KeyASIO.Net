using System.Diagnostics;
using System.Management;
using KeyAsio.Audio.Caching;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Milki.Extensions.Configuration;

namespace KeyAsio.Shared.Realtime;

public class SkinManager : IHostedService
{
    private static readonly Lock InstanceLock = new();
    private readonly ILogger<SkinManager> _logger;
    private readonly AppSettings _appSettings;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly SharedViewModel _sharedViewModel;
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;
    private bool _waiting;

    private readonly AsyncLock _asyncLock = new();

    private ManagementEventWatcher? _processWatcher;
    private CancellationTokenSource? _skinLoadCts;

    public SkinManager(ILogger<SkinManager> logger, AppSettings appSettings, AudioCacheManager audioCacheManager,
        SharedViewModel sharedViewModel)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioCacheManager = audioCacheManager;
        _sharedViewModel = sharedViewModel;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_appSettings.Paths.OsuFolderPath))
        {
            await CheckOsuRegistryAsync();
        }

        ListenPropertyChanging();
        _ = RefreshSkinsAsync();

        StartProcessListener();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopProcessListener();
        return Task.CompletedTask;
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
                _ = RefreshSkinsAsync();
            }
        };
    }

    public void StartProcessListener()
    {
        CheckAndSetOsuPath();

        try
        {
            // WMI 查询：监听 Win32_Process 的创建事件，且进程名为 osu!.exe
            // WITHIN 1 表示轮询间隔为 1 秒（WMI 内部机制，非代码循环）
            var query = new WqlEventQuery(
                "SELECT * FROM __InstanceCreationEvent WITHIN 3 WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = 'osu!.exe'");

            _processWatcher = new ManagementEventWatcher(query);
            _processWatcher.EventArrived += (s, e) =>
            {
                _logger.LogInformation("Detected osu! process start via WMI.");
                CheckAndSetOsuPath();
            };

            _processWatcher.Start();
            _logger.LogInformation("Osu process listener started.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WMI process watcher. Process detection may rely on restart.");
        }
    }

    private void StopProcessListener()
    {
        try
        {
            _processWatcher?.Stop();
            _processWatcher?.Dispose();
            _processWatcher = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping process watcher.");
        }
    }

    /// <summary>
    /// 核心逻辑：查找 osu! 进程并设置路径
    /// </summary>
    private void CheckAndSetOsuPath()
    {
        try
        {
            var processes = Process.GetProcessesByName("osu!");
            foreach (var proc in processes)
            {
                try
                {
                    if (proc.MainModule is not { } module) continue;

                    var fileName = module.FileName;
                    if (string.IsNullOrEmpty(fileName)) continue;

                    var fileVersionInfo = FileVersionInfo.GetVersionInfo(fileName);
                    if (fileVersionInfo.CompanyName == "ppy")
                    {
                        var detectedPath = Path.GetDirectoryName(Path.GetFullPath(fileName));

                        if (_appSettings.Paths.OsuFolderPath != detectedPath)
                        {
                            _logger.LogInformation("Auto-detected osu! path: {Path}", detectedPath);
                            UiDispatcher.Invoke(() => { _appSettings.Paths.OsuFolderPath = detectedPath; });
                        }

                        break;
                    }

                    if (fileVersionInfo.CompanyName == "ppy Pty Ltd")
                    {
                        // lazer wip
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // 忽略无权访问的进程
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error inspecting osu! process module.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckAndSetOsuPath.");
        }
    }

    public async Task RefreshSkinsAsync()
    {
        using var @lock = await _asyncLock.LockAsync();

        if (_skinLoadCts != null) await _skinLoadCts.CancelAsync();
        _skinLoadCts = new CancellationTokenSource();
        var token = _skinLoadCts.Token;

        _ = Task.Run(async () =>
        {
            //wip: wait mainwindow loaded?
            await Task.Delay(3000, token);
            LoadSkinsInternal(token);
        }, token);
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

    private void LoadSkinsInternal(CancellationToken token)
    {
        if (string.IsNullOrEmpty(_appSettings.Paths.OsuFolderPath)) return;
        var skinsDir = Path.Combine(_appSettings.Paths.OsuFolderPath, "Skins");
        if (!Directory.Exists(skinsDir)) return;

        var newSkinList = new List<SkinDescription> { SkinDescription.Default };

        var directories = Directory.EnumerateDirectories(skinsDir);
        var loadedSkins = directories.AsParallel()
            .WithDegreeOfParallelism(2)
            .Select(dir =>
            {
                if (token.IsCancellationRequested) return null;
                var iniPath = Path.Combine(dir, "skin.ini");
                string? name = null;
                string? author = null;
                if (File.Exists(iniPath))
                {
                    (name, author) = ReadIniFile(iniPath);
                }

                var skinDescription = new SkinDescription(Path.GetFileName(dir), dir, name, author);
                _logger.LogDebug("Find skin: {SkinDescription}", skinDescription);
                return skinDescription;
            })
            .Where(x => x != null)
            .ToList();

        if (token.IsCancellationRequested) return;

        newSkinList.AddRange(loadedSkins!);

        UiDispatcher.Invoke(() =>
        {
            if (token.IsCancellationRequested) return;

            _sharedViewModel.Skins.Clear();
            foreach (var s in newSkinList) _sharedViewModel.Skins.Add(s);

            var selectedName = _appSettings.Paths.SelectedSkinName;
            _sharedViewModel.SelectedSkin = _sharedViewModel.Skins
                                                .FirstOrDefault(k => k.FolderName == selectedName)
                                            ?? _sharedViewModel.Skins.FirstOrDefault();
        });
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
}