using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KeyAsio.Net.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum WavePlayerType
{
    DirectSound, WASAPI, ASIO
}