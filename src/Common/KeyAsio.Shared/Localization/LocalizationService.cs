using System.ComponentModel;
using System.Globalization;

namespace KeyAsio.Shared.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private Action<CultureInfo> _cultureApplier = static _ => { };
    private Func<string, string> _stringResolver = static key => key;
    private long _version;

    public static LocalizationService Instance { get; } = new();

    public string this[string key]
    {
        get
        {
            var resolver = _stringResolver;
            return resolver(key);
        }
    }

    /// <summary>
    ///     Monotonically increasing version counter; incremented on every language change.
    ///     Used by <see cref="I18NExtension" /> bindings to trigger re-evaluation.
    /// </summary>
    public long Version => _version;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ConfigureStringResolver(Func<string, string> resolver)
    {
        _stringResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public void ConfigureCultureApplier(Action<CultureInfo> cultureApplier)
    {
        _cultureApplier = cultureApplier ?? throw new ArgumentNullException(nameof(cultureApplier));
    }

    public void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        _cultureApplier(culture);
        NotifyLanguageChanged();
    }

    public void NotifyLanguageChanged()
    {
        Interlocked.Increment(ref _version);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
