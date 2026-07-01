using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KeyAsio.Shared.Localization;

public partial class LanguageManager : ObservableObject
{
    private const string SystemLanguageCode = "system";
    private readonly ILanguagePreferenceStore _languagePreferenceStore;
    private bool _isUpdating;

    public LanguageManager(ILanguagePreferenceStore languagePreferenceStore)
    {
        _languagePreferenceStore = languagePreferenceStore;
        InitializeLanguages();
    }

    public ObservableCollection<LanguageItem> AvailableLanguages { get; } = [];

    [ObservableProperty]
    public partial LanguageItem? SelectedLanguageItem { get; set; }

    [ObservableProperty]
    public partial bool IsChinese { get; set; }

    partial void OnSelectedLanguageItemChanged(LanguageItem? value)
    {
        if (value is null || _isUpdating)
        {
            return;
        }

        _languagePreferenceStore.SetLanguageCode(value.Code);
        ApplyLanguage(value.Code);
        RefreshAvailableLanguages(value.Code);
    }

    private void InitializeLanguages()
    {
        var persistedCode = _languagePreferenceStore.GetLanguageCode();
        var savedCode = string.IsNullOrWhiteSpace(persistedCode)
            ? SystemLanguageCode
            : persistedCode;

        ApplyLanguage(savedCode);

        _isUpdating = true;
        try
        {
            PopulateLanguages();
            SelectedLanguageItem = AvailableLanguages.FirstOrDefault(x =>
                                       string.Equals(x.Code, savedCode, StringComparison.OrdinalIgnoreCase))
                                   ?? AvailableLanguages[0];
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void RefreshAvailableLanguages(string selectedCode)
    {
        _isUpdating = true;
        try
        {
            // Deselect before clearing to avoid SelectionModel accessing a stale index.
            SelectedLanguageItem = null;
            AvailableLanguages.Clear();
            PopulateLanguages();
            SelectedLanguageItem =
                AvailableLanguages
                    .FirstOrDefault(x => string.Equals(x.Code, selectedCode, StringComparison.OrdinalIgnoreCase))
                ?? AvailableLanguages[0];
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void PopulateLanguages()
    {
        AvailableLanguages.Add(new LanguageItem
        {
            Name = LocalizationService.Instance["Language_SystemDefault"],
            Code = SystemLanguageCode
        });
        AvailableLanguages.Add(new LanguageItem
        {
            Name = CultureInfo.GetCultureInfo("zh-CN").NativeName,
            Code = "zh-CN"
        });
        AvailableLanguages.Add(new LanguageItem
        {
            Name = CultureInfo.GetCultureInfo("en").NativeName,
            Code = "en"
        });
    }

    private void ApplyLanguage(string languageCode)
    {
        var culture = ResolveCulture(languageCode);
        LocalizationService.Instance.ApplyCulture(culture);
        IsChinese = culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    private static CultureInfo ResolveCulture(string languageCode)
    {
        return string.Equals(languageCode, SystemLanguageCode, StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.InstalledUICulture
            : new CultureInfo(languageCode);
    }
}
