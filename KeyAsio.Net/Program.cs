using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;
using KeyAsio.Net.Audio;
using KeyAsio.Net.Models;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KeyAsio.Net
{
    class Program
    {
        internal static IKeyboardMouseEvents GlobalHook;
        private static DeviceDescription _deviceInfo;
        internal static IWavePlayer Device;

        public static AudioPlaybackEngine Engine { get; set; }

        [STAThread]
        static void Main(string[] args)
        {
            _handler = ConsoleEventCallback;
            GlobalHook = Hook.GlobalEvents();
            SetConsoleCtrlHandler(_handler, true);
            AppSettings settings;

            if (!File.Exists(AppSettings.SettingsPath))
            {
                settings = AppSettings.CreateNewConfig();
            }
            else
            {
                try
                {
                    var content = File.ReadAllText(AppSettings.SettingsPath);
                    settings = JsonConvert.DeserializeObject<AppSettings>(content,
                        new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.Auto,
                            ObjectCreationHandling = ObjectCreationHandling.Replace
                        }
                    );
                    AppSettings.LoadDefault(settings);
                }
                catch (JsonException ex)
                {
                    Console.Write($"Error occurs while loading settings:\r\n{ex.Message}\r\nGenerate a new one? (Y/n) ");
                    var yesNo = Console.ReadLine();
                    if (yesNo == "Y")
                    {
                        settings = AppSettings.CreateNewConfig();
                    }
                    else
                    {
                        return;
                    }
                }
            }

            _deviceInfo = settings.DeviceInfo ?? SelectDevice();
            Device = DeviceProvider.CreateDevice(out _deviceInfo, _deviceInfo, settings.Latency);
            Engine = new AudioPlaybackEngine(Device, AppSettings.Default.SampleRate, AppSettings.Default.ChannelCount);
            while (true)
            {
                if (Device == null)
                {
                    _deviceInfo = SelectDevice();
                    Device = DeviceProvider.CreateDevice(out _deviceInfo, _deviceInfo);
                    Engine = new AudioPlaybackEngine(Device, AppSettings.Default.SampleRate, AppSettings.Default.ChannelCount);
                }

                Console.WriteLine("Current Info: ");
                Console.WriteLine(JsonConvert.SerializeObject(new DisplayInfo
                {
                    DeviceInfo = _deviceInfo,
                    WaveFormat = Engine?.WaveFormat
                }, Formatting.Indented, new StringEnumConverter()));
                var formTrigger = new FormTrigger();
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
                            AppSettings.Default.DeviceInfo = null;
                            AppSettings.SaveDefault();
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
            GlobalHook.Dispose();
        }

        private static DeviceDescription SelectDevice()
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
            AppSettings.Default.DeviceInfo = selectedInfo;
            AppSettings.SaveDefault();
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
        public IDeviceInfo DeviceInfo { get; set; }
        public WaveFormat WaveFormat { get; set; }
    }
}
