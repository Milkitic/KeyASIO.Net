using KeyAsio.Net.Audio;
using KeyAsio.Net.Hooking;
using NAudio.Wave;
using Keys = KeyAsio.Net.Hooking.Keys;

namespace KeyAsio.Net
{
    public partial class FormTrigger : Form
    {
        private readonly AppSettings _settings;
        private readonly HashSet<Keys> _pressingKeys = new();
        private readonly KeyboardHookManager _keyboardHookManager;

        public FormTrigger(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();
            _keyboardHookManager = new KeyboardHookManager();
            Visible = false;
            Load += Form1_Load;
            Closed += Form1_Closed;
        }

        private async void Form1_Load(object? sender, EventArgs e)
        {
            var waveFormat = new WaveFormat(_settings.SampleRate, _settings.Bits, _settings.Channels);
            var cacheSound = await CachedSoundFactory.GetOrCreateCacheSound(waveFormat, _settings.HitsoundPath);
            _keyboardHookManager.Start();
            foreach (var key in _settings.Keys)
            {
                RegisterHotKey(key, cacheSound);
            }
        }

        private void Form1_Closed(object? sender, EventArgs e)
        {
            _keyboardHookManager.UnregisterAll();
            _keyboardHookManager.Stop();
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
                    Program.Engine.PlaySound(cacheSound);
                    Console.WriteLine($"{key} {action}");
                }
                else
                {
                    if (!_pressingKeys.Contains(key))
                        return;
                    _pressingKeys.Remove(key);
                    Console.WriteLine($"{key} {action}");
                }
            });
        }
    }
}
