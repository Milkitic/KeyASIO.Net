using Octokit;

namespace KeyAsio.Plugins.Abstractions;

public interface IUpdateImplementation
{
    Task StartUpdateAsync(Release release);
}