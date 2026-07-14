namespace KeyAsio.Plugins.Contracts;

public interface IUpdateSupportPlugin : IPlugin
{
    IUpdateImplementation UpdateImplementation { get; }
}