using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Octokit;

namespace KeyAsio.ViewModels;

[JsonSerializable(typeof(SupportersCache))]
internal partial class SupportersCacheContext : JsonSerializerContext
{
}

public partial class AboutPageViewModel : ViewModelBase
{
    public ObservableCollection<SupporterItem> Supporters { get; } = new();
    public ObservableCollection<ProjectItem> Projects { get; } = new();

    private const string RepoOwner = "Milkitic";
    private const string RepoName = "KeyAsio.Net";
    private const string CacheFileName = "supporters_v1.cache";

    public AboutPageViewModel()
    {
        //Supporters.Add(new SupporterItem("Sub User", "Gold Supporter"));
        //Supporters.Add(new SupporterItem("Coffee User", "Silver Supporter"));
        //Supporters.Add(new SupporterItem("Star User", "Supporter"));

        Projects.Add(new ProjectItem("Osu-Player",
            "A multifunctional media player for osu and osuer. Modern interface with WPF.",
            "https://github.com/Milkitic/Osu-Player"));
        Projects.Add(new ProjectItem("osu! Phalanx", "Under heavy development. Not available yet.",
            "https://github.com/Milkitic/osu-phalanx"));
        Projects.Add(new ProjectItem("KeyAsio.Plugins.LegacyFullMode",
            "The original music sync logic from v3. Available as a free, community-maintained plugin.",
            "https://github.com/Milkitic/KeyAsio.Plugins.LegacyFullMode"));

        // Initialize supporters in background
        Task.Run(InitializeSupportersAsync);
    }

    private async Task InitializeSupportersAsync()
    {
        var cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KeyAsio", CacheFileName);
        SupportersCache? cache = null;
        bool isCacheValid = false;

        try
        {
            if (File.Exists(cachePath))
            {
                var base64 = await File.ReadAllTextAsync(cachePath);
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                cache = JsonSerializer.Deserialize<SupportersCache>(json,
                    SupportersCacheContext.Default.SupportersCache);

                if (cache != null)
                {
                    // Cache validity: 12 hours
                    if ((DateTime.UtcNow - cache.LastUpdated).TotalHours < 12)
                    {
                        isCacheValid = true;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Ignore cache read errors
        }

        if (isCacheValid && cache != null)
        {
            UpdateSupportersUI(cache.Items);
            return;
        }

        try
        {
            var github = new GitHubClient(new ProductHeaderValue("KeyAsio.Net"));

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var task = github.Activity.Starring.GetAllStargazers(RepoOwner, RepoName);

            // Wait for task or timeout
            var completedTask = await Task.WhenAny(task, Task.Delay(-1, cts.Token));
            if (completedTask != task)
            {
                throw new TimeoutException("GitHub API request timed out.");
            }

            var stargazers = await task;

            var newItems = stargazers.Select(u => new SupporterCacheItem
            {
                Name = u.Login,
                Tier = "Stargazer" // Default tier for stargazers
            }).ToList();

            var newCache = new SupportersCache
            {
                LastUpdated = DateTime.UtcNow,
                Items = newItems
            };

            var json = JsonSerializer.Serialize(newCache, SupportersCacheContext.Default.SupportersCache);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            var tempPath = cachePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, base64);
            File.Move(tempPath, cachePath, overwrite: true);

            UpdateSupportersUI(newItems);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Network error: {ex.Message}");

            // Network fault tolerance: use expired cache if available
            if (cache != null)
            {
                UpdateSupportersUI(cache.Items);
            }
        }
    }

    private void UpdateSupportersUI(List<SupporterCacheItem> items)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Supporters.Clear();
            foreach (var item in items)
            {
                Supporters.Add(new SupporterItem(item.Name, item.Tier));
            }
        });
    }

    [RelayCommand]
    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore errors
        }
    }
}

public class SupporterItem
{
    public string Name { get; }
    public string Tier { get; }

    public SupporterItem(string name, string tier)
    {
        Name = name;
        Tier = tier;
    }
}

public class ProjectItem
{
    public string Name { get; }
    public string Description { get; }
    public string Url { get; }

    public ProjectItem(string name, string description, string url)
    {
        Name = name;
        Description = description;
        Url = url;
    }
}

public class SupportersCache
{
    public DateTime LastUpdated { get; set; }
    public List<SupporterCacheItem> Items { get; set; } = new();
}

public class SupporterCacheItem
{
    public string Name { get; set; } = "";
    public string Tier { get; set; } = "";
}