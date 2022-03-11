using System.Diagnostics.CodeAnalysis;

namespace KeyAsio.Net.Configuration;

public static class ConfigurationFactory
{
    public static bool TryLoadConfigFromFile<T>(string path,
        [NotNullWhen(true)] out T? config,
        [NotNullWhen(false)] out Exception? e,
        JsonConverter? converter = null)
        where T : ConfigurationBase
    {
        converter ??= new JsonConverter();
        var type = typeof(T);
        ConfigurationBase? retConfig;

        if (!Path.IsPathRooted(path))
            path = Path.Combine(Environment.CurrentDirectory, path);

        if (!File.Exists(path))
        {
            retConfig = CreateDefaultConfigByPath(type, path, converter);
            Console.WriteLine($"{path} config file not found. " +
                              $"Default config was created and used.");
        }
        else
        {
            var content = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(content)) content = "default:\r\n";
            try
            {
                retConfig = converter.DeserializeSettings(content, type);
                SaveConfig(retConfig, path, converter);
                Console.WriteLine($"{path} config file was loaded.");
            }
            catch (Exception ex)
            {
                retConfig = null;
                config = (T?)retConfig;
                e = ex;
                return false;
            }
        }

        e = null;
        config = (T)retConfig;
        config!.SaveAction = async () => SaveConfig(retConfig, path, converter);
        return true;
    }

    private static ConfigurationBase CreateDefaultConfigByPath(Type type, string path, JsonConverter converter)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, "");
        var config = converter.DeserializeSettings("default:\r\n", type);
        SaveConfig(config, path, converter);
        return config;
    }

    private static void SaveConfig(ConfigurationBase config, string path, JsonConverter converter)
    {
        var content = converter.SerializeSettings(config);
        File.WriteAllText(path, content, config.Encoding);
    }
}