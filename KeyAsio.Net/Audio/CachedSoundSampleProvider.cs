using System;
using NAudio.Wave;

namespace KeyAsio.Net.Audio
{
    class CachedSoundSampleProvider : ISampleProvider
    {
        public CachedSound SourceSound { get; }
        private long _position;

        public CachedSoundSampleProvider(CachedSound cachedSound)
        {
            SourceSound = cachedSound;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var availableSamples = SourceSound.AudioData.Length - _position;
            var samplesToCopy = Math.Min(availableSamples, count);
            Array.Copy(SourceSound.AudioData, _position, buffer, offset, samplesToCopy);
            _position += samplesToCopy;
            return (int)samplesToCopy;
        }

        public WaveFormat WaveFormat => SourceSound.WaveFormat;
    }
}