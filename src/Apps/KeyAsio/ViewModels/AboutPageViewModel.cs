using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;

namespace KeyAsio.ViewModels;

public partial class AboutPageViewModel : ViewModelBase
{
    public ObservableCollection<SupporterItem> Supporters { get; } = new();
    public ObservableCollection<ProjectItem> Projects { get; } = new();

    public AboutPageViewModel()
    {
        // Sample Data - You can replace these with real data
        //for (int i = 0; i < 10; i++)
        {
            Supporters.Add(new SupporterItem("Sub User", "Gold Supporter"));
            Supporters.Add(new SupporterItem("Coffee User", "Silver Supporter"));
            Supporters.Add(new SupporterItem("Star User", "Supporter"));
        }

        Projects.Add(new ProjectItem("Osu-Player", "A multifunctional media player for osu and osuer. Modern interface with WPF.",
            "https://github.com/Milkitic/Osu-Player"));
        Projects.Add(new ProjectItem("osu! Phalanx", "Under heavy development. Not available yet.",
            "https://github.com/Milkitic/osu-phalanx"));
        Projects.Add(new ProjectItem("KeyAsio.Plugins.LegacyFullMode", "The original music sync logic from v3. Available as a free, community-maintained plugin.",
            "https://github.com/Milkitic/KeyAsio.Plugins.LegacyFullMode"));
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