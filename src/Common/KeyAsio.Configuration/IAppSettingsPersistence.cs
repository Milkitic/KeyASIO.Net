using Milki.Extensions.Configuration;

namespace KeyAsio.Configuration;

public interface IAppSettingsPersistence
{
    void Save();
}

public sealed class AppSettingsPersistence : IAppSettingsPersistence
{
    private readonly AppSettings _settings;

    public AppSettingsPersistence(AppSettings settings)
    {
        _settings = settings;
    }

    public void Save() => _settings.Save();
}
