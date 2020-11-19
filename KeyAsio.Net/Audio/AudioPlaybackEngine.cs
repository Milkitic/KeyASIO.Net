using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyAsio.Net.Audio
{
    public class AudioPlaybackEngine : IDisposable
    {
        static ConcurrentDictionary<string, CachedSound> _cachedDictionary = new ConcurrentDictionary<string, CachedSound>();

        private readonly MixingSampleProvider _mixer;

        private static string[] _supportExtensions = { ".wav" };
        private readonly IWavePlayer _outputDevice;
        public WaveFormat WaveFormat { get; }
        
        public AudioPlaybackEngine(IWavePlayer outputDevice, int sampleRate = 44100, int channelCount = 2)
        {
            _outputDevice = outputDevice;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount);
            _mixer = new MixingSampleProvider(WaveFormat)
            {
                ReadFully = true
            };

            _outputDevice.Init(_mixer);
            _outputDevice.Play();
        }

        public void CreateCacheSounds(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                CreateCacheSound(path); // Cache each file once before play.
            }
        }

        public CachedSound GetOrCreateCacheSound(string path)
        {
            if (!_cachedDictionary.ContainsKey(path))
            {
                CreateCacheSound(path);
            }

            return _cachedDictionary[path];
        }

        public void CreateCacheSound(string path)
        {
            string newPath = path;
            if (!File.Exists(newPath))
            {
                newPath = TryGetPath(newPath);
            }

            if (!File.Exists(newPath))
            {
                newPath = TryGetPath(Path.Combine(Path.GetDirectoryName(newPath), Path.GetFileNameWithoutExtension(newPath)));
            }

            if (_cachedDictionary.ContainsKey(path))
            {
                return;
            }

            if (!File.Exists(newPath))
            {
                _cachedDictionary.TryAdd(path, null);
                return;
            }

            try
            {
                _cachedDictionary.TryAdd(path, new CachedSound(newPath)); // Cache each file once before play.
            }
            catch
            {
                _cachedDictionary.TryAdd(path, null);
            }
        }

        private static string TryGetPath(string path)
        {
            foreach (var ext in _supportExtensions)
            {
                var autoAudioFile = path + ext;
                if (!File.Exists(autoAudioFile))
                    continue;

                path = autoAudioFile;
                break;
            }

            return path;
        }

        public void PlaySound(string path)
        {
            if (!_cachedDictionary.ContainsKey(path))
            {
                CreateCacheSound(path);
            }

            PlaySound(_cachedDictionary[path]);
        }

        public void PlaySound(CachedSound sound)
        {
            if (sound == null) return;
            AddMixerInput(new CachedSoundSampleProvider(sound));
        }

        private void AddMixerInput(ISampleProvider input)
        {
            _mixer.AddMixerInput(input);
        }
        
        public void Dispose()
        {
            _outputDevice?.Dispose();
        }

        public static void ClearCacheSounds()
        {
            _cachedDictionary.Clear();
        }
    }
}