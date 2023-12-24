using System.Diagnostics;
using System.IO;
using KeyAsio.MemoryReading;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared.Models;
using Microsoft.Win32;
using Milki.Extensions.Configuration;
using Milki.Extensions.MixPlayer.NAudioExtensions.Wave;
using OsuMemoryDataProvider;

namespace KeyAsio.Shared.Realtime;

public class SkinManager
{
    private static readonly ILogger Logger = LogUtils.GetLogger("SkinManager");
    public static readonly SkinManager Instance = new();
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;
    private bool _waiting;

    private SkinManager()
    {
    }

    public SharedViewModel SharedViewModel => SharedViewModel.Instance;
    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public void ListenPropertyChanging()
    {
        SharedViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SharedViewModel.SelectedSkin))
            {
                AppSettings.SelectedSkin = SharedViewModel.SelectedSkin?.FolderName ?? "";
                CachedSoundFactory.ClearCacheSounds("internal");
            }
        };

        AppSettings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.OsuFolder))
            {
                RefreshSkinInBackground();
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

    public async void RefreshSkinInBackground()
    {
        lock (Instance)
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
            UiDispatcher.Invoke(() => SharedViewModel.Skins.Clear());
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
                    SharedViewModel.Skins.Add(skinDescription);
                }

                var selected = AppSettings.SelectedSkin;
                SharedViewModel.SelectedSkin = SharedViewModel.Skins.FirstOrDefault(k => k.FolderName == selected) ??
                                               SharedViewModel.Skins.FirstOrDefault();
            });
        });

        lock (Instance)
        {
            _waiting = false;
        }

        _refreshTask.Start();
    }

    public void CheckOsuRegistry()
    {
        try
        {
            var settings = AppSettings;
            using var reg = Registry.ClassesRoot.OpenSubKey(@"osu!\shell\open\command");
            if (reg == null) return;
            var parameters = reg.GetValue(null)?.ToString();
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
}