using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Utils;

public class RuntimeInfo
{
    public static bool IsSatori { get; private set; }

    public static void CheckCoreClr(ILogger<RuntimeInfo> logger)
    {
        var appDirectory = AppContext.BaseDirectory;

        var modules = Process.GetCurrentProcess().Modules;
        foreach (ProcessModule module in modules)
        {
            if (!module.ModuleName.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase)) continue;

            Console.WriteLine($"[CoreCLR Path] {module.FileName}");

            var moduleDir = Path.GetDirectoryName(module.FileName);
            var isLocal = string.Equals(moduleDir, appDirectory.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

            if (isLocal)
            {
                var hasSatori = ContainsStringReverse(module.FileName, "SatoriGC");
                if (hasSatori)
                {
                    logger.LogInformation("[CoreCLR Mode] Local Satori");
                    IsSatori = true;
                }
                else
                {
                    logger.LogInformation("[CoreCLR Mode] Local Original");
                }
            }
            else
            {
                logger.LogInformation("[CoreCLR Mode] Shared Original");
            }
        }
    }

    private static bool ContainsStringReverse(string filePath, string targetStr)
    {
        if (string.IsNullOrEmpty(targetStr)) return false;

        var targetArray = Encoding.UTF8.GetBytes(targetStr);
        var targetSpan = targetArray.AsSpan();

        const int bufferSize = 4096;
        var buffer = new byte[bufferSize];

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var fileLength = fs.Length;
        if (fileLength < targetArray.Length) return false;

        var position = fileLength;

        while (position > 0)
        {
            var readStart = position - bufferSize;
            var bytesToRead = bufferSize;

            if (readStart < 0)
            {
                bytesToRead = (int)position;
                readStart = 0;
            }

            fs.Seek(readStart, SeekOrigin.Begin);
            var readCount = fs.Read(buffer, 0, bytesToRead);

            ReadOnlySpan<byte> bufferSpan = buffer.AsSpan(0, readCount);

            if (bufferSpan.IndexOf(targetSpan) >= 0)
            {
                return true;
            }

            if (readStart == 0) break;
            position -= (bufferSize - targetArray.Length + 1);
        }

        return false;
    }
}