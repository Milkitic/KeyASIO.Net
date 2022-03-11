using System.Text.Json;
using System.Text.Json.Serialization;
using KeyAsio.Net.Audio;
using KeyAsio.Net.Hooking;
using KeyAsio.Net.Models;
using NAudio.Wave;
using Keys = KeyAsio.Net.Hooking.Keys;

namespace KeyAsio.Net
{
    public partial class FormTrigger : Form
    {
        private readonly AppSettings _settings;

        private readonly HashSet<Keys> _pressingKeys = new();
        private readonly KeyboardHookManager _keyboardHookManager;

        private readonly DeviceDescription? _deviceDescription;
        private IWavePlayer _device;
        private AudioPlaybackEngine _engine;

        public FormTrigger(DeviceDescription? deviceDescription, AppSettings settings)
        {
            InitializeComponent();
            _keyboardHookManager = new KeyboardHookManager();
            _deviceDescription = deviceDescription;
            _settings = settings;

            Load += Form1_Load;
            Closed += Form1_Closed;
        }

        private async void Form1_Load(object? sender, EventArgs e)
        {
            CreateDevice();

            if (_device is AsioOut asioOut)
            {
                btnAsio.Enabled = true;
            }

            var waveFormat = new WaveFormat(_settings.SampleRate, _settings.Bits, _settings.Channels);
            var cacheSound = await CachedSoundFactory.GetOrCreateCacheSound(waveFormat, _settings.HitsoundPath);
            _keyboardHookManager.Start();
            foreach (var key in _settings.Keys)
            {
                RegisterHotKey(key, cacheSound);
            }

            Console.WriteLine("Your active keys: " + string.Join(",", _settings.Keys.OrderBy(k => k)));
            Console.WriteLine("Initialization done.");
        }

        private void Form1_Closed(object? sender, EventArgs e)
        {
            _keyboardHookManager.UnregisterAll();
            _keyboardHookManager.Stop();

            _engine?.Dispose();
            _device?.Stop();
            _device?.Dispose();
        }

        private void CreateDevice()
        {
            _device = DeviceProvider.CreateDevice(out var actualDeviceInfo, _deviceDescription);
            _engine = new AudioPlaybackEngine(_device, _settings.SampleRate, _settings.Channels);

            Console.WriteLine("Active device information: ");
            var aymInfo = new
            {
                DeviceInfo = actualDeviceInfo,
                WaveFormat = _engine?.WaveFormat
            };
            Console.WriteLine(JsonSerializer.Serialize(aymInfo, new JsonSerializerOptions()
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            }));
            Console.WriteLine();
        }

        private void RegisterHotKey(Keys key, CachedSound? cacheSound)
        {
            _keyboardHookManager.RegisterHotkey(key, action =>
            {
                if (action == CallBackType.Down)
                {
                    if (_pressingKeys.Contains(key))
                        return;
                    _pressingKeys.Add(key);
                    _engine.PlaySound(cacheSound);
                    if (_settings.Debugging)
                        Console.WriteLine($"{key} {action}");
                }
                else
                {
                    if (!_pressingKeys.Contains(key))
                        return;
                    _pressingKeys.Remove(key);
                    if (_settings.Debugging)
                        Console.WriteLine($"{key} {action}");
                }
            });
        }

        private void btnAsio_Click(object sender, EventArgs e)
        {
            if (_device is AsioOut asioOut)
            {
                asioOut.ShowControlPanel();
            }
        }
    }
}