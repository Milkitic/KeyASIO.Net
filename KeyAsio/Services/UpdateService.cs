using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Octokit;
using Semver;
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