using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace KeyAsio.Shared.Utils;

public static class OsuLocator
{
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
}