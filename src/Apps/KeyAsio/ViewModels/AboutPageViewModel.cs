using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;

namespace KeyAsio.ViewModels;

public partial class AboutPageViewModel : ViewModelBase
{
    public ObservableCollection<SponsorItem> Sponsors { get; } = new();
    public ObservableCollection<ProjectItem> Projects { get; } = new();

    public AboutPageViewModel()
    {
        // Sample Data - You can replace these with real data
        for (int i = 0; i < 10; i++)
        {
            Sponsors.Add(new SponsorItem("Sponsor User 1", "Gold Sponsor"));
            Sponsors.Add(new SponsorItem("Sponsor User 2", "Silver Sponsor"));
            Sponsors.Add(new SponsorItem("Sponsor User 3", "Supporter"));
        }

        Projects.Add(new ProjectItem("KeyASIO.Net", "A low-latency audio solution for rhythm games.",
            "https://github.com/KeyASIO/KeyASIO.Net"));
        Projects.Add(new ProjectItem("ProMix Plugin", "Professional mixing plugin for KeyASIO.",
            "https://github.com/KeyAsio/KeyAsio.Plugins.ProMix"));
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

public class SponsorItem
{
    public string Name { get; }
    public string Tier { get; }

    public SponsorItem(string name, string tier)
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