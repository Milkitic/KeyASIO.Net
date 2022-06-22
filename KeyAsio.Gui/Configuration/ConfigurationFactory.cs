using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using KeyAsio.Gui.Configuration.Converters;
using KeyAsio.Gui.Utils;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Gui.Configuration;

public static class ConfigurationFactory
{
    private static readonly Dictionary<Type, ConfigurationInformation> InformationDictionary = new();
    private static readonly Dictionary<string, ConfigurationInformation> InformationDictionary2 = new();
    private static readonly Dictionary<object, ConfigurationInformation> InstanceDictionary = new();
    private static readonly ILogger Logger = SharedUtils.GetLogger(nameof(ConfigurationFactory));

    public static T GetConfiguration<T>(IConfigurationConverter? converter = null, string? baseFolder = null)
    {
        return GetConfiguration<T>(baseFolder, converter);
    }

    public static T GetConfiguration<T>(string? baseFolder, IConfigurationConverter? converter = null)
    {
        var t = typeof(T);

        if (InformationDictionary.TryGetValue(t, out var val))
            return (T)val.Instance;

        var name = (t.GetCustomAttribute<ConfigurationAttribute>()?.Name ?? t.Name).ToLower();
        var filename = name + ".yaml";
        return GetConfiguration<T>(true, baseFolder, filename, converter);
    }

    public static T GetConfiguration<T>(string? baseFolder, string filename, IConfigurationConverter? converter = null)
    {
        baseFolder ??= Path.GetFullPath(".");
        var path = Path.GetFullPath(Path.Combine(baseFolder, filename));
        if (InformationDictionary2.TryGetValue(path, out var val))
            return (T)val.Instance;

        return GetConfiguration<T>(false, baseFolder, filename, converter);
    }

    public static T GetConfiguration<T>(bool isFromType, string? baseFolder, string filename, IConfigurationConverter? converter = null)
    {
        var t = typeof(T);
        var name = Path.GetFileNameWithoutExtension(filename);
        var encodingString = t.GetCustomAttribute<EncodingAttribute>()?.EncodingString;
        var encoding = encodingString == null ? Encoding.UTF8 : Encoding.GetEncoding(encodingString);

        baseFolder ??= Path.GetFullPath(".");
        var path = Path.GetFullPath(Path.Combine(baseFolder, filename));
        converter ??= YamlConverter.Instance;
        var success = TryLoadConfigFromFile<T>(path, converter, encoding, out var config, out var ex);
        if (!success) throw ex!;

        var information = new ConfigurationInformation(name, filename, path, baseFolder, config!, encoding, converter);
        if (isFromType)
        {
            InformationDictionary.Add(t, information);
        }
        else
        {
            InformationDictionary2.Add(path, information);
        }

        InstanceDictionary.Add(config!, information);
        return config!;
    }

    public static bool TryLoadConfigFromFile<T>(string path,
        IConfigurationConverter converter,
        Encoding encoding,
        out T? config,
        out Exception? e)
    {
        var success = TryLoadConfigFromFile(typeof(T), path, converter, encoding, out var config1, out e);
        config = (T?)config1;
        return success;
    }

    public static bool TryLoadConfigFromFile(Type type,
        string path,
        IConfigurationConverter converter,
        Encoding encoding,
        out object? config,
        out Exception? e)
    {
        if (!Path.IsPathRooted(path))
            path = Path.Combine(Environment.CurrentDirectory, path);

        if (!File.Exists(path))
        {
            config = CreateDefaultConfigByPath(type, path, converter, encoding);
            Logger.LogWarning($"Config file \"{Path.GetFileName(path)}\" was not found. " +
                              $"Default config was created and used.");
        }
        else
        {
            var content = File.ReadAllText(path, encoding);
            if (string.IsNullOrWhiteSpace(content)) content = "default:\r\n";
            try
            {
                config = converter.DeserializeSettings(content, type);
                SaveConfig(config, path, converter, encoding);
                Logger.LogDebug($"Config file \"{Path.GetFileName(path)}\" was loaded.");
            }
            catch (Exception ex)
            {
                config = null;
                e = ex;
                return false;
            }
        }

        e = null;
        return true;
    }

    public static object CreateDefaultConfigByPath(Type type, string path, IConfigurationConverter converter,
        Encoding encoding)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, "", encoding);
        var config = converter.DeserializeSettings("default:\r\n", type);
        SaveConfig(config, path, converter, encoding);
        return config;
    }

    public static void SaveConfig(object config, string path, IConfigurationConverter converter,
        Encoding encoding)
    {
        var content = converter.SerializeSettings(config);
        File.WriteAllText(path, content, encoding);
    }

    public static void Save(object config)
    {
        if (!InstanceDictionary.TryGetValue(config, out var information)) return;

        var name = information.Name;
        var encoding = information.Encoding;
        var converter = information.Converter;
        var path = information.Path;

        var newPath = Path.Combine(information.BaseFolder, name + "." + Path.GetRandomFileName() + ".yaml");
        SaveConfig(config, newPath, converter, encoding);
        File.Copy(newPath, path, true);
        File.Delete(newPath);
    }
}

public class ConfigurationAttribute : Attribute
{
    public ConfigurationAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
}