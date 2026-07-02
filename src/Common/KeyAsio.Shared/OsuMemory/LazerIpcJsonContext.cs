using System.Text.Json.Serialization;

namespace KeyAsio.Shared.OsuMemory;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(LazerIpcFrame))]
[JsonSerializable(typeof(LazerIpcFile))]
internal partial class LazerIpcJsonContext : JsonSerializerContext;
