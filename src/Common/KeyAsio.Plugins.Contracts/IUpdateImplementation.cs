namespace KeyAsio.Plugins.Contracts;

public interface IUpdateImplementation
{
    Task StartUpdateAsync(UpdateRelease release, CancellationToken cancellationToken = default);
}
