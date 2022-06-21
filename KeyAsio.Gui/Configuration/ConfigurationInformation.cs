using System.Text;

namespace KeyAsio.Gui.Configuration;

internal class ConfigurationInformation
{
    public ConfigurationInformation(string name, string filename, string path, string baseFolder,
        object instance,
        Encoding encoding, IConfigurationConverter converter)
    {
        Name = name;
        Filename = filename;
        Path = path;
        BaseFolder = baseFolder;
        Instance = instance;
        Encoding = encoding;
        Converter = converter;
    }

    public string Name { get; }
    public string Filename { get; }
    public string Path { get; }
    public string BaseFolder { get; }
    public object Instance { get; }
    public Encoding Encoding { get; }
    public IConfigurationConverter Converter { get; }
}