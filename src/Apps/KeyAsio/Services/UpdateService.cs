using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared;
using KeyAsio.Shared.OsuMemory;
using Microsoft.Extensions.Logging;
using Octokit;
using Semver;

namespace KeyAsio.Services;

public partial class UpdateService : ObservableObject
{
    private const string RepoOwner = "Milkitic";
    private const string RepoName = "KeyAsio.Net";

    private const string RulesUrl =
        "https://raw.githubusercontent.com/Milkitic/KeyASIO.Net/refs/heads/master/osu_memory_rules.json";

    private readonly ILogger<UpdateService> _logger;
    private readonly MemoryScan _memoryScan;
    private readonly AppSettings _appSettings;
    private readonly GitHubClient _github;
    private readonly HttpClient _httpClient = new();

    public IUpdateImplementation UpdateImplementation { get; set; } = new BasicUpdateImplementation();

    public UpdateService(ILogger<UpdateService> logger, MemoryScan memoryScan, AppSettings appSettings)
    {
        _logger = logger;
        _memoryScan = memoryScan;
        _appSettings = appSettings;
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

    [ObservableProperty]
    public partial bool IsRulesUpdateAvailable { get; private set; }

    private string? _newRulesContent;

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
            var channel = _appSettings.Update.Channel;
            var latest = releases
                .OrderByDescending(k => k.PublishedAt)
                .FirstOrDefault(k =>
                {
                    if (k.Draft) return false;
                    if (channel == UpdateChannel.Stable && k.Prerelease) return false;
                    return true;
                });

            if (latest == null)
            {
                NewRelease = null;
                StatusMessage = "No updates available (latest release not found).";
                return false;
            }

            var remoteVersion = latest.TagName.TrimStart('v');

            // Use loose parsing to be more robust
            if (!SemVersion.TryParse(remoteVersion, SemVersionStyles.Any, out var remoteSemVersion))
            {
                _logger.LogError("Failed to parse remote version: {Remote}", remoteVersion);
                StatusMessage = $"Error parsing remote version: {remoteVersion}";
                return null;
            }

            _logger.LogDebug("Current version: {NowVerObj}; Got version info: {LatestVerObj}", SemVersion,
                remoteSemVersion);

            if (remoteSemVersion.ComparePrecedenceTo(SemVersion) <= 0)
            {
                NewRelease = null;
                NewVersion = null;
                NewSemVersion = null;
                StatusMessage = "You are using the latest version.";
                return false;
            }

            // Map Octokit Release to UpdateUtils.GithubRelease to maintain compatibility
            NewRelease = latest;
            NewVersion = FixCommit(remoteVersion);
            NewSemVersion = remoteSemVersion;
            StatusMessage = null; // Clear status message when update is available
            return true;
        }
        catch (RateLimitExceededException)
        {
            _logger.LogError("Error while checking for updates: Github API rate limit exceeded. Please retry later.");
            StatusMessage = "Github API rate limit exceeded.";
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for updates");
            StatusMessage = "Error checking for updates.";
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

        await UpdateImplementation.StartUpdateAsync(NewRelease);
    }

    public async Task<bool> CheckRulesUpdateAsync()
    {
        try
        {
            var rulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "osu_memory_rules.json");
            var remoteRules = await _httpClient.GetStringAsync(RulesUrl);

            if (!File.Exists(rulesPath))
            {
                _newRulesContent = remoteRules;
                IsRulesUpdateAvailable = true;
                return true;
            }

            var localRules = await File.ReadAllTextAsync(rulesPath);

            var localNode = JsonNode.Parse(localRules);
            var remoteNode = JsonNode.Parse(remoteRules);

            if (!JsonNode.DeepEquals(localNode, remoteNode))
            {
                _newRulesContent = remoteRules;
                IsRulesUpdateAvailable = true;
                return true;
            }

            IsRulesUpdateAvailable = false;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check rules update");
            return false;
        }
    }

    public async Task UpdateRulesAsync()
    {
        if (_newRulesContent == null) return;
        try
        {
            var rulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "osu_memory_rules.json");
            await File.WriteAllTextAsync(rulesPath, _newRulesContent);
            IsRulesUpdateAvailable = false;
            _newRulesContent = null;

            _memoryScan.ReloadRules();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update rules");
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