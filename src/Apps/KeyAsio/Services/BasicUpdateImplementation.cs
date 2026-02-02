using System.Diagnostics;
using KeyAsio.Plugins.Abstractions;
using Octokit;

namespace KeyAsio.Services;

public class BasicUpdateImplementation : IUpdateImplementation
{
    public Task StartUpdateAsync(Release release)
    {
        if (release.HtmlUrl != null)
        {
            Process.Start(new ProcessStartInfo(release.HtmlUrl) { UseShellExecute = true });
        }

        return Task.CompletedTask;
    }
}