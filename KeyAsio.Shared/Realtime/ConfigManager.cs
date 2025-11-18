using Coosu.Shared.IO;
using KeyAsio.MemoryReading.Logging;
using Milki.Extensions.Configuration;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.Shared.Realtime;

internal class ConfigManager
{
    public static readonly ConfigManager Instance = new();

    private ConfigManager()
    {
    }

    public HookKeys KeyOsuLeft { get; private set; }
    public HookKeys KeyOsuRight { get; private set; }
    public HookKeys KeyTaikoInnerLeft { get; private set; }
    public HookKeys KeyTaikoInnerRight { get; private set; }
    public HookKeys KeyTaikoOuterLeft { get; private set; }
    public HookKeys KeyTaikoOuterRight { get; private set; }

    public AppSettings AppSettings => ConfigurationFactory.GetConfiguration<AppSettings>();

    public void ReadConfigs()
    {
        if (AppSettings.OsuFolder == null) return;
        var cfgFile = Path.Combine(AppSettings.OsuFolder, GetUserConfigFilename(Environment.UserName));
        using var fs = File.Open(cfgFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sr = new StreamReader(fs);

        while (sr.ReadLine() is { } line)
        {
            var span = line.AsSpan();
            var s = span.IndexOf('=');

            if (span.Length == 0 || span[0] == '#' || s < 0) continue;

            var key = span[..s].Trim();
            var value = span[(s + 1)..].Trim();

            if (key.Equals("keyOsuLeft", StringComparison.Ordinal))
            {
                KeyOsuLeft = Enum.Parse<HookKeys>(value);
            }
            else if (key.Equals("keyOsuRight", StringComparison.Ordinal))
            {
                KeyOsuRight = Enum.Parse<HookKeys>(value);
            }
        }
    }

    private static string GetUserConfigFilename(string username)
    {
        return $"osu!.{PathUtils.EscapeFileName(username)}.cfg";
    }
}