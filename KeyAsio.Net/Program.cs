using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using KeyAsio.Net.Audio;
using KeyAsio.Net.Configuration;
using KeyAsio.Net.Models;
using NAudio.Wave;

namespace KeyAsio.Net;

class Program
{
    private static DeviceDescription _deviceInfo;
    internal static IWavePlayer Device;

    public static AudioPlaybackEngine Engine { get; set; }

    static async Task Main(string[] args)
    {
        _handler = ConsoleEventCallback;
        SetConsoleCtrlHandler(_handler, true);

        string settingsPath = args.Length > 0 ? args[0] : "./appsettings.json";
        if (!ConfigurationFactory.TryLoadConfigFromFile<AppSettings>(settingsPath,
                out var settings, out var exception))
        {
            throw exception;
        }

        _deviceInfo = settings.Device ?? await SelectDevice(settings);
        Device = DeviceProvider.CreateDevice(out _deviceInfo, _deviceInfo);
        Engine = new AudioPlaybackEngine(Device, settings.SampleRate, settings.Channels);
        while (true)
        {
            if (Device == null)
            {
                _deviceInfo = await SelectDevice(settings);
                Device = DeviceProvider.CreateDevice(out _deviceInfo, _deviceInfo);
                Engine = new AudioPlaybackEngine(Device, settings.SampleRate, settings.Channels);
            }

            Console.WriteLine("Current Info: ");
            Console.WriteLine(JsonSerializer.Serialize(new DisplayInfo
            {
                DeviceInfo = _deviceInfo,
                WaveFormat = Engine?.WaveFormat
            }));
            var formTrigger = new FormTrigger(settings);
            Application.Run(formTrigger);

            Console.WriteLine("Type \"asio\" to open asio GUI control panel.");
            Console.WriteLine("Type \"device\" to select a device.");
            Console.WriteLine("Type \"exit\" to close program.");
            Console.WriteLine("Else reopen the window.");
            var o = Console.ReadLine();
            switch (o)
            {
                case "panel":
                    if (Device is AsioOut ao)
                    {
                        ao.ShowControlPanel();
                    }
                    else
                    {
                        Console.WriteLine("Output method is not ASIO.");
                    }

                    break;
                case "device":
                    Console.Write("Stop current device and select a new one ? (Y/n) ");
                    var yesNo = Console.ReadLine();
                    if (yesNo == "Y")
                    {
                        Device?.Dispose();
                        Engine?.Dispose();
                        Device = null;
                        Engine = null;
                        settings.Device = null;
                        await settings.SaveAsync();
                    }
                    else
                    {
                        Console.WriteLine("Canceled operation.");
                    }

                    break;
                case "exit":
                    DisposeAll();
                    Device = null;
                    Engine = null;
                    formTrigger.Close();
                    return;
            }

            Console.WriteLine();
        }
    }

    private static void DisposeAll()
    {
        Device?.Dispose();
        Engine?.Dispose();
    }

    private static async Task<DeviceDescription> SelectDevice(AppSettings settings)
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

        sb.Append($"(default {selectedIndex}) ");

        selectedIndex = ReadIndex(sb.ToString(), o.Count, selectedIndex);
        var selected = o.FirstOrDefault(k => k.Value == selectedIndex);

        var dic = devices
            .Where(k => k.WavePlayerType == selected.Key).Select((k, i) => (i + 1, k))
            .ToDictionary(k => k.k, k => k.Item1);
        sb.Clear();
        sb.AppendLine(string.Join("\r\n", dic.Select(k => $"{k.Value}. {k.Key.FriendlyName}")));
        sb.Append("Select output device: (default 1)");

        selectedIndex = ReadIndex(sb.ToString(), dic.Count, 1);
        var selectedInfo = dic.FirstOrDefault(k => k.Value == selectedIndex).Key;
        settings.Device = selectedInfo;
        await settings.SaveAsync();
        return selectedInfo;
    }

    private static int ReadIndex(string info, int maxIndex, int def)
    {
        Console.Write(info);
        var numStr = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(numStr))
        {
            Console.WriteLine();
            return def;
        }

        int i;
        while (!int.TryParse(numStr, out i) || i > maxIndex || i < 1)
        {
            Console.WriteLine();
            Console.WriteLine("Sorry, please input a valid index.");
            Console.Write(info);
            numStr = Console.ReadLine();
        }

        Console.WriteLine();
        return i;
    }

    private static bool ConsoleEventCallback(int eventType)
    {
        if (eventType == 2)
        {
            Console.WriteLine(Environment.NewLine + "Console window closing, death imminent...");
            DisposeAll();
            Thread.Sleep(500);
        }

        return false;
    }

    private static ConsoleEventDelegate _handler;

    private delegate bool ConsoleEventDelegate(int eventType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
}

internal class DisplayInfo
{
    public DeviceDescription DeviceInfo { get; set; }
    public WaveFormat WaveFormat { get; set; }
}