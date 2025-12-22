using System;
using BenchmarkDotNet.Attributes;

namespace SimdBenchmarks
{
    // For more information on the VS BenchmarkDotNet Diagnosers see https://learn.microsoft.com/visualstudio/profiling/profiling-with-benchmark-dotnet
    //[CPUUsageDiagnoser]
    [MemoryDiagnoser]
    public class SimdAudioConverterBenchmark
    {
        private readonly Random _random = new(1996);

        private byte[] _bytes;
        private short[] _shorts;
        private float[] _floats;

        [GlobalSetup]
        public void Setup()
        {
            _bytes = new byte[1024 * 1024];
            _random.NextBytes(_bytes);

            _shorts = new short[_bytes.Length / sizeof(short)];
            _floats = new float[_shorts.Length];
            Buffer.BlockCopy(_bytes, 0, _shorts, 0, _bytes.Length);
        }

        [Benchmark(Baseline = true)]
        public unsafe float[] Old()
        {
            fixed (short* ps = _shorts)
            fixed (float* pf = _floats)
            {
                Legacy.SimdAudioConverter.Convert16BitToFloatUnsafe(ps, pf, _floats.Length);
            }

            return _floats;
        }

        [Benchmark]
        public unsafe float[] New()
        {
            fixed (short* ps = _shorts)
            fixed (float* pf = _floats)
            {
                KeyAsio.Audio.Utils.SimdAudioConverter.Convert16BitToFloatUnsafe(ps, pf, _floats.Length);
            }

            return _floats;
        }
    }
}
