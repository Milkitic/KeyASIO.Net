using System;

namespace KeyAsio.Gui.Configuration;

public interface IConfigurationConverter
{
    public object DeserializeSettings(string content, Type type);
    public string SerializeSettings(object obj);
}