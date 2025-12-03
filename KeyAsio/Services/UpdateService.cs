using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Octokit;
using Semver;
using SharpCompress.Archives;
using SharpCompress.Common;
using ProductHeaderValue = Octokit.ProductHeaderValue;

namespace KeyAsio.Services;

[ObservableObject]
public partial class UpdateService
{
    private const string RepoOwner = "Milkitic";
    private const string RepoName = "KeyAsio.Net";

    private readonly ILogger<UpdateService> _logger;
    private readonly GitHubClient _github;

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
        _github = new GitHubClient(new ProductHeaderValue("KeyAsio.Net"));
        InitializeVersion();
    }

    [ObservableProperty]
    public partial bool IsRunningChecking { get; private set; }

    [ObservableProperty]
    public partial Release? NewRelease { get; private set; }

    [ObservableProperty]
    public partial SemVersion? SemVersion { get; private set; }

    [ObservableProperty]
    public partial string? Version { get; private set; }

    [ObservableProperty]
    public partial SemVersion? NewSemVersion { get; private set; }

    [ObservableProperty]
    public partial string? NewVersion { get; private set; }

    [ObservableProperty]
    public partial double DownloadProgress { get; private set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; private set; }

    public Action? UpdateAction { get; set; }
    public Action<bool?>? CheckUpdateCallback { get; set; }

    [RelayCommand]
    public async Task CheckForUpdates()
    {
        var result = await CheckUpdateAsync();
        CheckUpdateCallback?.Invoke(result);
    }

    [RelayCommand]
    public void TriggerUpdate()
    {
        UpdateAction?.Invoke();
    }

    private CancellationTokenSource? _updateCts;

    public void CancelUpdate()
    {
        _updateCts?.Cancel();
    }

    public async Task<bool?> CheckUpdateAsync()
    {
        if (IsRunningChecking) return null;
        if (SemVersion == null) return null;
        IsRunningChecking = true;

        try
        {
            var releases = await _github.Repository.Release.GetAll(RepoOwner, RepoName);

            // Replicate original logic: Pick the latest non-draft release.
            // Note: GetAll() returns paginated results (default 30). This is usually sufficient to find the latest non-draft.
            var latest = releases
                .OrderByDescending(k => k.PublishedAt)
                .FirstOrDefault(k => !k.Draft /*&& !k.Prerelease*/);

            if (latest == null)
            {
                NewRelease = null;
                return false;
            }

            var remoteVersion = latest.TagName.TrimStart('v');

            // Use loose parsing to be more robust
            if (!SemVersion.TryParse(remoteVersion, SemVersionStyles.Any, out var remoteSemVersion))
            {
                _logger.LogError("Failed to parse remote version: {Remote}", remoteVersion);
                return null;
            }

            _logger.LogDebug("Current version: {NowVerObj}; Got version info: {LatestVerObj}", SemVersion,
                remoteSemVersion);

            if (remoteSemVersion.ComparePrecedenceTo(SemVersion) <= 0)
            {
                NewRelease = null;
                NewVersion = null;
                NewSemVersion = null;
                return false;
            }

            // Map Octokit Release to UpdateUtils.GithubRelease to maintain compatibility
            NewRelease = latest;
            NewVersion = FixCommit(remoteVersion);
            NewSemVersion = remoteSemVersion;
            return true;
        }
        catch (RateLimitExceededException)
        {
            _logger.LogError("Error while checking for updates: Github API rate limit exceeded. Please retry later.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for updates");
            return null;
        }
        finally
        {
            IsRunningChecking = false;
        }
    }

    public async Task DownloadAndInstallAsync()
    {
        if (NewRelease == null) return;

        _updateCts = new CancellationTokenSource();
        var token = _updateCts.Token;

        // 1. Find Asset
        // Prioritize win64/x64 if running on 64-bit
        var is64Bit = Environment.Is64BitOperatingSystem;
        var asset = NewRelease.Assets.FirstOrDefault(a => a.Name.Contains(is64Bit ? "win64" : "win32", StringComparison.OrdinalIgnoreCase) && (a.Name.EndsWith(".zip") || a.Name.EndsWith(".7z")));

        if (asset == null)
        {
            // Fallback to first zip/7z
            asset = NewRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip") || a.Name.EndsWith(".7z"));
        }

        if (asset == null)
        {
            _logger.LogError("No suitable asset found for release {Version}", NewRelease.TagName);
            StatusMessage = "No suitable download found.";
            return;
        }

        try
        {
            StatusMessage = "Downloading...";
            DownloadProgress = 0;
            var tempFile = Path.GetTempFileName();

            using (var client = new HttpClient())
            {
                using (var response = await client.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? 1L;
                    await using (var stream = await response.Content.ReadAsStreamAsync(token))
                    await using (var fileStream = File.Create(tempFile))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int read;
                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read, token);
                            totalRead += read;
                            DownloadProgress = (double)totalRead / totalBytes * 100;
                        }
                    }
                }
            }

            token.ThrowIfCancellationRequested();
            StatusMessage = "Extracting...";
            var updateDir = Path.Combine(Path.GetTempPath(), "KeyAsioUpdate_" + Path.GetRandomFileName());
            Directory.CreateDirectory(updateDir);

            using (var archive = ArchiveFactory.Open(tempFile))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    token.ThrowIfCancellationRequested();
                    await entry.WriteToDirectoryAsync(updateDir, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    }, token);
                }
            }

            token.ThrowIfCancellationRequested();
            // Handle nested folder structure if present
            var subDirs = Directory.GetDirectories(updateDir);
            var files = Directory.GetFiles(updateDir);
            string sourceDir = updateDir;

            // If there's only one directory and no files, assume it's a wrapper folder
            if (files.Length == 0 && subDirs.Length == 1)
            {
                sourceDir = subDirs[0];
            }

            StatusMessage = "Restarting...";

            // Create Updater Script
            var currentProcess = Process.GetCurrentProcess();
            var appPath = AppContext.BaseDirectory;
            var exeName = Path.GetFileName(currentProcess.MainModule?.FileName ?? "KeyAsio.exe");

            var pid = currentProcess.Id;
            var scriptPath = Path.Combine(Path.GetTempPath(), "update_script.ps1");

            // PowerShell script to wait, copy, and restart
            var script = $@"
$procId = {pid}
$source = '{sourceDir}'
$dest = '{appPath}'
$exe = '{Path.Combine(appPath, exeName)}'

Write-Host 'Waiting for KeyAsio to exit...'
try {{
    Wait-Process -Id $procId -Timeout 10 -ErrorAction SilentlyContinue
}} catch {{}}

Write-Host 'Updating files...'
Copy-Item -Path ""$source\*"" -Destination ""$dest"" -Recurse -Force

Write-Host 'Starting KeyAsio...'
Start-Process -FilePath ""$exe""
";
            await File.WriteAllTextAsync(scriptPath, script, token);

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
            Environment.Exit(0);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Canceled.";
            _logger.LogInformation("Update canceled by user.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed");
            StatusMessage = "Update Failed: " + ex.Message;
        }
        finally
        {
            _updateCts?.Dispose();
            _updateCts = null;
        }
    }

    public void OpenLastReleasePage()
    {
        if (NewRelease?.HtmlUrl == null) return;
        Process.Start(new ProcessStartInfo(NewRelease.HtmlUrl) { UseShellExecute = true });
    }

    private void InitializeVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        version ??= "0.0.0";
        if (!SemVersion.TryParse(version, SemVersionStyles.Any, out var currentVersion))
        {
            _logger.LogError("Failed to parse local version: {Local}", version);
            return;
        }

        Version = FixCommit(version);
        SemVersion = currentVersion;
    }

    private static string? FixCommit(string? version)
    {
        if (version == null) return version;
        var lastIndexOf = version.LastIndexOf('+');
        if (lastIndexOf < 0) return version;

        // Handle commit hash suffixes if present (e.g., 1.0.0+abc1234)
#if DEBUG
        if (version.Length > lastIndexOf + 8)
        {
            return version.Substring(0, lastIndexOf + 8);
        }

        return version;
#else
        return version.Substring(0, lastIndexOf);
#endif
    }
}