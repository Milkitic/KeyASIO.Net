using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyAsio.Memory.Configuration;

public class MemoryProfile
{
    [JsonPropertyName("signatures")]
    public Dictionary<string, string> Signatures { get; set; } = new();

    [JsonPropertyName("pointers")]
    public Dictionary<string, PointerDefinition> Pointers { get; set; } = new();

    [JsonPropertyName("values")]
    public Dictionary<string, ValueDefinition> Values { get; set; } = new();

    public static MemoryProfile Load(string path)
    {
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };
        return JsonSerializer.Deserialize<MemoryProfile>(json, options) ?? new MemoryProfile();
    }
}

public class PointerDefinition
{
    [JsonPropertyName("base")]
    public string Base { get; set; } = string.Empty;

    [JsonPropertyName("offsets")]
    public List<int> Offsets { get; set; } = new();
}

public class ValueDefinition
{
    [JsonPropertyName("base")]
    public string Base { get; set; } = string.Empty;

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "int"; // int, float, double, bool, short, string
}