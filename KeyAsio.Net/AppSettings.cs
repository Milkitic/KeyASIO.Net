using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace KeyAsio.Net
{
    class AppSettings
    {
        public HashSet<Keys> Keys { get; set; } = new HashSet<Keys>
        {
            System.Windows.Forms.Keys.A,
            System.Windows.Forms.Keys.X
        };
        public string HitsoundPath { get; set; } = "click.wav";
        public int Latency { get; set; } = 0;
        public int SampleRate { get; set; } = 48000;
        public int Bits { get; set; } = 16;
        public int ChannelCount { get; set; } = 2;
        public IDeviceInfo DeviceInfo { get; set; }
        public void Save()
        {
            lock (FileSaveLock)
            {
                //FileStream.Value.SetLength(0);
                var content = JsonConvert.SerializeObject(this, Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
                        ObjectCreationHandling = ObjectCreationHandling.Replace
                    });
                //byte[] buffer = Encoding.GetBytes(content);
                //FileStream.Value.Write(buffer, 0, buffer.Length);
                File.WriteAllText(SettingsPath, content);
            }
        }

        private static readonly object FileSaveLock = new object();
        public static string SettingsPath { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        public static AppSettings Default { get; private set; }

        public static void SaveDefault()
        {
            Default?.Save();
        }

        public static void LoadDefault(AppSettings config)
        {
            Default = config ?? new AppSettings();
            //Default.FileStream = File.Open(Domain.ConfigFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public static AppSettings CreateNewConfig()
        {
            var settings = LoadNew();
            SaveDefault();
            return settings;
        }

        private static AppSettings LoadNew()
        {
            File.WriteAllText(SettingsPath, "");
            var appSettings = new AppSettings();
            LoadDefault(appSettings);
            return appSettings;
        }
    }
}
