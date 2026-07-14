namespace KeyAsio.Application.Localization;

public interface ILanguagePreferenceStore
{
    string? GetLanguageCode();

    void SetLanguageCode(string languageCode);
}
