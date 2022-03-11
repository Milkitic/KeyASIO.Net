using System.Text;
using System.Text.Json.Serialization;

namespace KeyAsio.Net.Configuration;

public class ConfigurationBase
{
    [JsonIgnore]
    public virtual Encoding Encoding { get; } = Encoding.UTF8;

    [JsonIgnore]
    internal Func<Task>? SaveAction;

    public async Task SaveAsync()
    {
        if (SaveAction != null) await SaveAction();
    }
}