using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyAsio.Memory.Configuration;

[JsonSourceGenerationOptions(WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip,
    PropertyNameCaseInsensitive = true, AllowTrailingCommas = true, Converters = [typeof(HexIntJsonConverter)])]
[JsonSerializable(typeof(MemoryProfile))]
internal partial class SourceGenerationContext : JsonSerializerContext { }

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
        var profile = JsonSerializer.Deserialize<MemoryProfile>(json, SourceGenerationContext.Default.MemoryProfile) ?? new MemoryProfile();
        profile.Link();
        return profile;
    }

    public void Link()
    {
        foreach (var kvp in Pointers)
        {
            var ptr = kvp.Value;
            ptr.Name = kvp.Key;

            if (Pointers.TryGetValue(ptr.Base, out var parent))
            {
                ptr.ParentPointer = parent;
            }
        }

        foreach (var val in Values.Values)
        {
            if (Pointers.TryGetValue(val.Base, out var parent))
            {
                val.ParentPointer = parent;
            }
        }
    }
}

public class HexIntJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (string.IsNullOrEmpty(stringValue)) return 0;

            var parseString = stringValue;
            bool isNegative = false;

            if (parseString.StartsWith('-'))
            {
                isNegative = true;
                parseString = parseString.Substring(1);
            }

            if (parseString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var val = Convert.ToInt32(parseString, 16);
                return isNegative ? -val : val;
            }

            return int.Parse(stringValue);
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} for integer.");
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public class PointerDefinition
{
    [JsonPropertyName("base")]
    public string Base { get; set; } = string.Empty;

    [JsonPropertyName("offsets")]
    public List<int> Offsets { get; set; } = new();

    [JsonIgnore]
    public string Name { get; set; } = string.Empty;

    [JsonIgnore]
    public PointerDefinition? ParentPointer { get; set; }

    // Runtime Cache
    [JsonIgnore]
    public IntPtr CachedAddress;

    [JsonIgnore]
    public long CachedTick;
}

public class ValueDefinition
{
    [JsonPropertyName("base")]
    public string Base { get; set; } = string.Empty;

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "int"; // int, float, double, bool, short, string

    [JsonIgnore]
    public PointerDefinition? ParentPointer { get; set; }
}