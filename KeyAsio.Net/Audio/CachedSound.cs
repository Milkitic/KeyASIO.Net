using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace KeyAsio.Net.Audio
{
    public class CachedSound
    {
        public float[] AudioData { get; private set; }
        public WaveFormat WaveFormat { get; private set; }

        public CachedSound(string audioFileName)
        {
            using (var audioFileReader = WaveFormatFactory.GetResampledFileReader(audioFileName))
            {
                var wholeData = new List<float>((int)(audioFileReader.Length / 4));

                var readBuffer =
                    new float[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels];
                int samplesRead;
                while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    wholeData.AddRange(readBuffer.Take(samplesRead));
                }

                AudioData = wholeData.ToArray();
                WaveFormat = audioFileReader.WaveFormat;

            }
        }
    }
}