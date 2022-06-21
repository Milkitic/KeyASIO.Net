using System.Runtime.InteropServices;
using System.Text;
using KeyAsio.Net.Audio;
using KeyAsio.Net.Configuration;
using KeyAsio.Net.Models;

namespace KeyAsio.Net;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        _handler = ConsoleEventCallback;
        SetConsoleCtrlHandler(_handler, true);
        ApplicationConfiguration.Initialize();
        Application.ThreadException += Application_ThreadException;

        string settingsPath = args.Length > 0 ? args[0] : "./appsettings.yaml";
        if (!ConfigurationFactory.TryLoadConfigFromFile<AppSettings>(settingsPath, out var settings, out var exception))
        {
            throw exception;
        }

        try
        {
            DialogLoop(settings);
        }
        finally
        {
            Dispose();
        }
    }

    private static void DialogLoop(AppSettings settings)
    {
        while (true)
        {
            var deviceInfo = settings.Device ?? SelectDevice(settings);
            var formTrigger = new FormTrigger(deviceInfo, settings);
            Application.Run(formTrigger);
            var sb = new StringBuilder(Environment.NewLine);
            sb.AppendLine("1. Reopen the window.");
            sb.AppendLine("2. Reselect device.");
            sb.AppendLine("3. Exit.");
            sb.Append("Select operation: ");
            var selectedIndex = 1;
            sb.Append($"(default {selectedIndex}) ");

            selectedIndex = ReadIndex(sb.ToString(), 3, selectedIndex);
            switch (selectedIndex)
            {
                case 2:
                    settings.Device = null;
                    settings.Save();
                    break;
                case 3:
                    formTrigger.Close();
                    return;
            }

            Console.WriteLine();
        }
    }

    private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        throw e.Exception;
    }

    private static DeviceDescription SelectDevice(AppSettings settings)
    {
        var devices = DeviceProvider.GetCachedAvailableDevices().ToList();
        var o = devices
            .GroupBy(k => k.WavePlayerType)
            .Select((k, i) => (i + 1, k.Key))
            .ToDictionary(k => k.Key, k => k.Item1);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join("\r\n", o.Select(k => $"{k.Value}. {k.Key}")));
        sb.Append("Select output method: ");
        int selectedIndex;
        if (o.ContainsKey(WavePlayerType.ASIO))
        {
            selectedIndex = o[WavePlayerType.ASIO];
        }
        else if (o.ContainsKey(WavePlayerType.WASAPI))
        {
            selectedIndex = o[WavePlayerType.WASAPI];
        }
        else
        {
            selectedIndex = 1;
        }

        sb.Append($"(default {selectedIndex}) >");

        selectedIndex = ReadIndex(sb.ToString(), o.Count, selectedIndex);
        var selected = o.FirstOrDefault(k => k.Value == selectedIndex);

        var dic = devices
            .Where(k => k.WavePlayerType == selected.Key).Select((k, i) => (i + 1, k))
            .ToDictionary(k => k.k, k => k.Item1);
        sb.Clear();
        sb.AppendLine(string.Join("\r\n", dic.Select(k => $"{k.Value}. {k.Key.FriendlyName}")));
        sb.Append("Select output device: (default 1) >");

        selectedIndex = ReadIndex(sb.ToString(), dic.Count, 1);
        var selectedInfo = dic.FirstOrDefault(k => k.Value == selectedIndex).Key;
        settings.Device = selectedInfo;
        settings.Save();
        return selectedInfo;
    }

    private static int ReadIndex(string info, int maxIndex, int def)
    {
        Console.Write(info);
        var numStr = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(numStr))
        {
            return def;
        }

        int i;
        while (!int.TryParse(numStr, out i) || i > maxIndex || i < 1)
        {
            Console.WriteLine();
            Console.WriteLine("Sorry, please input a valid index.");
            Console.WriteLine();
            Console.Write(info);
            numStr = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(numStr))
            {
                return def;
            }
        }

        Console.WriteLine();
        return i;
    }

    private static void Dispose()
    {
    }

    private static bool ConsoleEventCallback(int eventType)
    {
        if (eventType == 2)
        {
            Console.WriteLine(Environment.NewLine + "Exiting...");
            Dispose();
            Thread.Sleep(100);
        }

        return true;
    }

    private static ConsoleEventDelegate _handler;

    private delegate bool ConsoleEventDelegate(int eventType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
}