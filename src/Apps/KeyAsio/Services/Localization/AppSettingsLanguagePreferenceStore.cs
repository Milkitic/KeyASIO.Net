using KeyAsio.Shared;
using KeyAsio.Shared.Localization;
using Milki.Extensions.Configuration;

namespace KeyAsio.Services.Localization;

public sealed class AppSettingsLanguagePreferenceStore : ILanguagePreferenceStore
{
    private readonly AppSettings _appSettings;

    public AppSettingsLanguagePreferenceStore(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public string? GetLanguageCode()
    {
        return _appSettings.General.Language;
    }

    public void SetLanguageCode(string languageCode)
    {
        _appSettings.General.Language = languageCode;
        _appSettings.Save();
    }
}
