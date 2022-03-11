using System.Text.Json.Serialization;

namespace KeyAsio.Net.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WavePlayerType
{
    DirectSound, WASAPI, ASIO
}