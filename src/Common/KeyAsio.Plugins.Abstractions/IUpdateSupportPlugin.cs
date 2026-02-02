namespace KeyAsio.Plugins.Abstractions;

public interface IUpdateSupportPlugin : IPlugin
{
    IUpdateImplementation UpdateImplementation { get; }
}