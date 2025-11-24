using Coosu.Shared.IO;
using Milki.Extensions.MouseKeyHook;

namespace KeyAsio.Shared.Realtime;

internal class ConfigManager
{
    private readonly YamlAppSettings _appSettings;

    private ConfigManager(YamlAppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public HookKeys KeyOsuLeft { get; private set; }
    public HookKeys KeyOsuRight { get; private set; }
    public HookKeys KeyTaikoInnerLeft { get; private set; }
    public HookKeys KeyTaikoInnerRight { get; private set; }
    public HookKeys KeyTaikoOuterLeft { get; private set; }
    public HookKeys KeyTaikoOuterRight { get; private set; }

    public void ReadConfigs()
    {
        if (_appSettings.Paths.OsuFolderPath == null) return;
        var cfgFile = Path.Combine(_appSettings.Paths.OsuFolderPath, GetUserConfigFilename(Environment.UserName));
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