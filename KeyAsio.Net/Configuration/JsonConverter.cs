using System.Text.Json;

namespace KeyAsio.Net.Configuration;

public class JsonConverter
{
    public ConfigurationBase DeserializeSettings(string content, Type type)
    {
        var obj = JsonSerializer.Deserialize(content, type, new JsonSerializerOptions
        {
            //DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        });
        return (ConfigurationBase)obj;
    }

    public string SerializeSettings(object obj)
    {
        var content = JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            //DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            WriteIndented = true
        });
        return content;
    }
}