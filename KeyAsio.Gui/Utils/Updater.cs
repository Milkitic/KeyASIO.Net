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
using KeyAsio.Gui.Configuration;
using Microsoft.Extensions.Logging;
using Semver;

namespace KeyAsio.Gui.Utils;

public static class Updater
{
    private static readonly ILogger Logger = SharedUtils.GetLogger(nameof(Updater));
    private const string Repo = "Milkitic/KeyAsio.Net";
    private static string? _version;
    private const int Timeout = 10000;
    private const int RetryCount = 3;
    private static readonly HttpClient HttpClient;

    public static UpdateUtils.GithubRelease? NewRelease { get; private set; }
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
                Logger.LogError("Error while checking for updates: Github API rate limit exceeded.");
                return null;
            }

            var releases = JsonSerializer.Deserialize<List<UpdateUtils.GithubRelease>>(json)!;
            var latest = releases
                .OrderByDescending(k => k.PublishedAt)
                .FirstOrDefault(k => !k.Draft /*&& !k.PreRelease*/);
            if (latest == null)
            {
                NewRelease = null;
                return false;
            }

            var latestVer = latest.TagName.TrimStart('v');

            var latestVerObj = SemVersion.Parse(latestVer, SemVersionStyles.Strict);
            var nowVerObj = SemVersion.Parse(GetVersion(), SemVersionStyles.Strict);

            Logger.LogDebug($"Current version: {nowVerObj}; Got version info: {latestVerObj}");

            if (latestVerObj.ComparePrecedenceTo(nowVerObj) <= 0)
            {
                NewRelease = null;
                return false;
            }

            NewRelease = latest;
            NewRelease.NewVerString = latestVer;
            NewRelease.NowVerString = GetVersion();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error while checking for updates: {ex.Message}");
            return null;
            //throw;
        }

        IsRunningChecking = false;
        return true;
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
            "ozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.100 Safari/537.36");
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

    public static void OpenLastReleasePage()
    {
        if (NewRelease != null)
        {
            Process.Start(new ProcessStartInfo($"https://github.com/{Repo}/releases/tag/v{NewRelease.NewVerString}")
            {
                UseShellExecute = true
            });
        }
    }
}