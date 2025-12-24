using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Markup.Xaml.MarkupExtensions;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyAsio.Shared;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Services;

public class LanguageItem
{
    public string Name { get; set; } = "";
    public string? Code { get; set; }

    public override string ToString() => Name;
}

public partial class LanguageManager : ObservableObject
{
    private readonly ILogger<LanguageManager> _logger;
    private readonly AppSettings _appSettings;

    public LanguageManager(ILogger<LanguageManager> logger, AppSettings appSettings)
    {
        _logger = logger;
        _appSettings = appSettings;
        InitializeLanguages();
    }

    [ObservableProperty]
    public partial LanguageItem? SelectedLanguageItem { get; set; }

    public ObservableCollection<LanguageItem> AvailableLanguages { get; } = new();

    partial void OnSelectedLanguageItemChanged(LanguageItem? value)
    {
        if (value != null)
        {
            _appSettings.General.Language = value.Code;
            ApplyLanguage(value.Code);
        }
    }

    private void ApplyLanguage(string? languageCode)
    {
        try
        {
            var culture = string.IsNullOrEmpty(languageCode)
                ? CultureInfo.InstalledUICulture
                : new CultureInfo(languageCode);

            I18NExtension.Culture = culture;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set language to {Language}", languageCode);
        }
    }

    private void InitializeLanguages()
    {
        AvailableLanguages.Clear();
        AvailableLanguages.Add(new LanguageItem { Name = "System Default", Code = null });

        var executablePath = AppDomain.CurrentDomain.BaseDirectory;
        try
        {
            var files = Directory.EnumerateFiles(executablePath, "KeyAsio.resources.dll", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var dirName = Path.GetFileName(Path.GetDirectoryName(file));
                try
                {
                    // Check if it's a valid culture directory
                    var culture = CultureInfo.GetCultureInfo(dirName);
                    // Check if it contains resources for this app
                    if (AvailableLanguages.Any(l => l.Code == dirName)) continue;

                    AvailableLanguages.Add(new LanguageItem
                    {
                        Name = culture.NativeName,
                        Code = dirName
                    });
                }
                catch
                {
                    // Ignore non-culture directories
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to scan for languages");
        }

        // Set initial selection
        var currentLang = _appSettings.General.Language;
        var selected = AvailableLanguages.FirstOrDefault(l => l.Code == currentLang)
                       ?? AvailableLanguages.FirstOrDefault(l => l.Code == null); // Default to System Default

        SelectedLanguageItem = selected;
        OnPropertyChanged(nameof(SelectedLanguageItem));

        // Apply the language immediately
        if (selected != null)
        {
            ApplyLanguage(selected.Code);
        }
    }
}