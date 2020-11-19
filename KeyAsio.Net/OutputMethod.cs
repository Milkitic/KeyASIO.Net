using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KeyAsio.Net
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OutputMethod
    {
        DirectSound, Wasapi, Asio
    }
}