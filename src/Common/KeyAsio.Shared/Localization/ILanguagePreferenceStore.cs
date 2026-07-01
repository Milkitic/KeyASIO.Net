namespace KeyAsio.Shared.Localization;

public interface ILanguagePreferenceStore
{
    string? GetLanguageCode();

    void SetLanguageCode(string languageCode);
}
