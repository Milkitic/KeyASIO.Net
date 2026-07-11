using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace KeyAsio.Application.Utils;

public static class OsuLocator
{
    public const string LazerExeName = "osu!";
    public const string LazerProcessName = "osu!";

    [SupportedOSPlatform("windows")]
    public static string? FindFromRegistry()
    {
        using var reg = Registry.ClassesRoot.OpenSubKey(@"osu!\shell\open\command");
        var parameters = reg?.GetValue(null)?.ToString();
        if (string.IsNullOrWhiteSpace(parameters)) return null;

        var path = parameters.Replace(" \"%1\"", "").Trim(' ', '"');
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        return Directory.Exists(dir) ? dir : null;
    }

    public static string? FindFromRunningProcess(Process[]? processes = null)
    {
        processes ??= Process.GetProcessesByName("osu!");
        string? result = null;

        foreach (var proc in processes)
        {
            try
            {
                if (result != null) continue;

                if (proc.HasExited) continue;
                if (proc.MainModule is not { } module) continue;

                var fileName = module.FileName;
                if (string.IsNullOrEmpty(fileName)) continue;

                var fileVersionInfo = FileVersionInfo.GetVersionInfo(fileName);
                if (fileVersionInfo.CompanyName == "ppy")
                {
                    var detectedPath = Path.GetDirectoryName(Path.GetFullPath(fileName));
                    if (Directory.Exists(detectedPath))
                    {
                        result = detectedPath;
                    }
                }
                else if (fileVersionInfo.CompanyName == "ppy Pty Ltd")
                {
                    // lazer wip
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Ignore access denied
            }
            finally
            {
                proc.Dispose();
            }
        }

        return result;
    }

    /// <summary>
    /// Find the executable directory of a running osu!lazer process.
    /// Returns null if lazer is not running or the directory cannot be determined.
    /// </summary>
    public static string? FindLazerExeDirectoryFromRunningProcess(Process[]? processes = null)
    {
        processes ??= Process.GetProcessesByName(LazerProcessName);
        string? result = null;

        foreach (var proc in processes)
        {
            try
            {
                if (result != null) continue;
                if (proc.HasExited) continue;
                if (proc.MainModule is not { } module) continue;

                var fileName = module.FileName;
                if (string.IsNullOrEmpty(fileName)) continue;

                var fileVersionInfo = FileVersionInfo.GetVersionInfo(fileName);
                if (fileVersionInfo.CompanyName == "ppy Pty Ltd")
                {
                    var detectedPath = Path.GetDirectoryName(Path.GetFullPath(fileName));
                    if (Directory.Exists(detectedPath))
                    {
                        result = detectedPath;
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Ignore access denied
            }
            finally
            {
                proc.Dispose();
            }
        }

        return result;
    }

    /// <summary>
    /// Find the osu!lazer user data directory.
    /// Lazer stores the user-selected data path in `storage.ini` (FullPath key),
    /// which is located next to the lazer executable. If no custom path is set,
    /// lazer uses %LOCALAPPDATA%/osu! on Windows.
    /// </summary>
    public static string? FindLazerUserDataDirectory(string? lazerExeDirectory = null)
    {
        if (lazerExeDirectory != null)
        {
            var configured = ReadLazerCustomStoragePath(lazerExeDirectory);
            if (configured != null && Directory.Exists(configured))
            {
                return configured;
            }
        }

        // Default location on Windows: %LOCALAPPDATA%/osu!
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var defaultPath = Path.Combine(localAppData, "osu!");
            if (Directory.Exists(defaultPath))
            {
                return defaultPath;
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    /// <summary>
    /// Read the custom storage path from lazer's storage.ini next to the executable.
    /// Returns the path or null if not set / file missing.
    /// </summary>
    public static string? ReadLazerCustomStoragePath(string lazerExeDirectory)
    {
        var storageIniPath = Path.Combine(lazerExeDirectory, "storage.ini");
        if (!File.Exists(storageIniPath))
        {
            return null;
        }

        try
        {
            using var sr = new StreamReader(storageIniPath);
            string? line;
            string? fullPath = null;

            while ((line = sr.ReadLine()) != null)
            {
                var trimmed = line.AsSpan().Trim();
                if (trimmed.IsEmpty) continue;

                // Skip section headers like [StorageConfig]
                if (trimmed[0] == '[') continue;

                var eq = trimmed.IndexOf('=');
                if (eq < 0) continue;

                var key = trimmed.Slice(0, eq).Trim();
                if (key.Equals("FullPath", StringComparison.OrdinalIgnoreCase))
                {
                    var value = trimmed.Slice(eq + 1).Trim().ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        fullPath = value;
                    }

                    break;
                }
            }

            return fullPath;
        }
        catch
        {
            return null;
        }
    }
}
