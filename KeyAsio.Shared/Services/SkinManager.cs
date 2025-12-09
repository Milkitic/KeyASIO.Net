using System.Diagnostics;
using KeyAsio.Audio.Caching;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Milki.Extensions.Configuration;

namespace KeyAsio.Shared.Services;

public class SkinManager
{
    private readonly ILogger<SkinManager> _logger;
    private readonly AppSettings _appSettings;
    private readonly AudioCacheManager _audioCacheManager;
    private readonly SharedViewModel _sharedViewModel;

    private readonly AsyncLock _asyncLock = new();

    private CancellationTokenSource? _processPollingCts;
    private Task? _processPollingTask;
    private CancellationTokenSource? _skinLoadCts;
    private Task? _skinLoadTask;

    public SkinManager(ILogger<SkinManager> logger, AppSettings appSettings, AudioCacheManager audioCacheManager,
        SharedViewModel sharedViewModel)
    {
        _logger = logger;
        _appSettings = appSettings;
        _audioCacheManager = audioCacheManager;
        _sharedViewModel = sharedViewModel;
    }

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
        foreach (var proc in processes)
        {
            try
            {
                if (proc.HasExited) continue;
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
                        _appSettings.Paths.OsuFolderPath = detectedPath;
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

    private async Task RefreshSkinsAsync()
    {
        using var @lock = await _asyncLock.LockAsync();

        await StopRefreshTask();
        _skinLoadCts = new CancellationTokenSource();
        var token = _skinLoadCts.Token;

        _skinLoadTask = Task.Run(() => LoadSkinsInternal(token));
    }

    private void CheckOsuRegistry()
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

        var directories = Directory.EnumerateDirectories(skinsDir);
        var loadedSkins = directories
            .AsParallel()
            .WithDegreeOfParallelism(2)
            .Select(dir =>
            {
                if (token.IsCancellationRequested) return null!;
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
            .Where(x => x != null!)
            .ToList();

        var newSkinList = new List<SkinDescription> { SkinDescription.Default };
        newSkinList.AddRange(loadedSkins);

        var selectedName = _appSettings.Paths.SelectedSkinName;
        var targetSkin = newSkinList.FirstOrDefault(k => k.FolderName == selectedName)
                         ?? SkinDescription.Default;

        _ = UiDispatcher.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested) return;

            _sharedViewModel.Skins.Clear();
            _sharedViewModel.Skins.AddRange(newSkinList);
            _sharedViewModel.SelectedSkin = targetSkin;
        });
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

    private async Task StopRefreshTask()
    {
        if (_skinLoadCts != null)
        {
            await _skinLoadCts.CancelAsync();
            _skinLoadCts.Dispose();
        }

        if (_skinLoadTask != null)
        {
            await _skinLoadTask;
        }
    }
}