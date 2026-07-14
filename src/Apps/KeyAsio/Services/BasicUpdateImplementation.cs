using System.Diagnostics;
using KeyAsio.Plugins.Contracts;

namespace KeyAsio.Services;

public class BasicUpdateImplementation : IUpdateImplementation
{
    public Task StartUpdateAsync(UpdateRelease release, CancellationToken cancellationToken = default)
    {
        if (release.ReleasePageUrl != null)
        {
            Process.Start(new ProcessStartInfo(release.ReleasePageUrl) { UseShellExecute = true });
        }

        return Task.CompletedTask;
    }
}
