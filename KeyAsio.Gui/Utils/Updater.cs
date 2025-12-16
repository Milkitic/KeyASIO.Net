using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KeyAsio.MemoryReading.Logging;
using KeyAsio.Shared;
using Semver;

namespace KeyAsio.Gui.Utils;

public static class Updater
{
    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(Updater));
    private const string Repo = "Milkitic/KeyAsio.Net";
    private static string? _version;
    private const int Timeout = 10000;
    private const int RetryCount = 3;
    private static readonly HttpClient HttpClient;

    public static UpdateUtils.GithubRelease? NewV3Release { get; private set; }
    public static UpdateUtils.GithubRelease? NewV4Release { get; private set; }
    public static bool IsRunningChecking { get; private set; }

    public static string GetVersion()
    {
        if (_version != null) return _version;

        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        _version = version ?? "";
        return _version;
    }

    public static async Task<bool?> CheckUpdateAsync()
    {
        IsRunningChecking = true;
        NewV3Release = null;
        NewV4Release = null;

        try
        {
            string? json = "";
            while (json == "")
            {
                json = await HttpGetAsync($"https://api.github.com/repos/{Repo}/releases");
            }

            if (json == null) return null;
            if (json.Contains("API rate limit"))
            {
                Logger.Error("Error while checking for updates: Github API rate limit exceeded. Please retry later.");
                return null;
            }

            var releases = JsonSerializer.Deserialize<List<UpdateUtils.GithubRelease>>(json)!;
            var validReleases = releases
                .Where(k => !k.Draft)
                .OrderByDescending(k => k.PublishedAt)
                .ToList();

            if (!validReleases.Any())
            {
                return false;
            }

            var nowVerObj = SemVersion.Parse(GetVersion(), SemVersionStyles.Strict);
            var v4VerObj = SemVersion.Parse("4.0.0", SemVersionStyles.Any);

            // Check for V4
            var latestV4 = validReleases.FirstOrDefault(k =>
            {
                try
                {
                    var ver = SemVersion.Parse(k.TagName.TrimStart('v'), SemVersionStyles.Any);
                    return ver.ComparePrecedenceTo(v4VerObj) >= 0;
                }
                catch
                {
                    return false;
                }
            });

            if (latestV4 != null)
            {
                latestV4.NewVerString = latestV4.TagName.TrimStart('v');
                NewV4Release = latestV4;
            }

            // Check for V3 update
            var latestV3 = validReleases.FirstOrDefault(k =>
            {
                try
                {
                    var ver = SemVersion.Parse(k.TagName.TrimStart('v'), SemVersionStyles.Any);
                    return ver.ComparePrecedenceTo(v4VerObj) < 0;
                }
                catch
                {
                    return false;
                }
            });

            if (latestV3 != null)
            {
                var v3VerObj = SemVersion.Parse(latestV3.TagName.TrimStart('v'), SemVersionStyles.Any);
                if (v3VerObj.ComparePrecedenceTo(nowVerObj) > 0)
                {
                    latestV3.NewVerString = v3VerObj.ToString();
                    latestV3.NowVerString = GetVersion();
                    NewV3Release = latestV3;
                }
            }

            Logger.LogDebug(
                $"Current version: {nowVerObj}; V3 Update: {NewV3Release?.NewVerString}; V4 Found: {NewV4Release?.NewVerString}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error while checking for updates: {ex.Message}", true);
            return null;
            //throw;
        }

        IsRunningChecking = false;
        return NewV3Release != null || NewV4Release != null;
    }

    static Updater()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        HttpClient =
            new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip })
            {
                Timeout = TimeSpan.FromMilliseconds(Timeout)
            };
        HttpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.100 Safari/537.36");
    }

    private static async Task<string?> HttpGetAsync(string url)
    {
        for (int i = 0; i < RetryCount; i++)
        {
            try
            {
                var message = new HttpRequestMessage(HttpMethod.Get, url);
                using var cts = new CancellationTokenSource(Timeout);
                var response = await HttpClient.SendAsync(message, cts.Token).ConfigureAwait(false);
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                if (i == RetryCount - 1)
                    throw;
            }
        }

        return null;
    }

    public static void OpenLastV3ReleasePage()
    {
        if (NewV3Release != null)
        {
            Process.Start(new ProcessStartInfo($"https://github.com/{Repo}/releases/tag/v{NewV3Release.NewVerString}")
            {
                UseShellExecute = true
            });
        }
    }

    public static void OpenLastV4ReleasePage()
    {
        if (NewV4Release != null)
        {
            Process.Start(new ProcessStartInfo($"https://github.com/{Repo}/releases/tag/v{NewV4Release.NewVerString}")
            {
                UseShellExecute = true
            });
        }
    }

    public static void OpenCommonReleasePage()
    {
        Process.Start(new ProcessStartInfo($"https://github.com/{Repo}/releases")
        {
            UseShellExecute = true
        });
    }
}