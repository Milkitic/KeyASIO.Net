using System.Buffers;
using System.Runtime.InteropServices;
using ManagedBass;
using NAudioWaveFormat = NAudio.Wave.WaveFormat;

namespace KeyAsio.Core.Audio.Wave;

internal static class BassAudioDecoder
{
    private const int BassConfigMp3OldGaps = 68;
    private const int DecodeBufferBytes = 64 * 1024;

    private static readonly Lock s_initLock = new();

    private static bool s_initializationAttempted;
    private static bool s_initialized;
    private static Exception? s_initializationException;

    public static DecodedPcmAudio Decode(byte[] sourceData, int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        EnsureInitialized();

        int stream = Bass.CreateStream(sourceData, 0, length, BassFlags.Decode | BassFlags.Prescan);
        if (stream == 0)
        {
            throw CreateBassException("Failed to create BASS decode stream.");
        }

        try
        {
            var info = Bass.ChannelGetInfo(stream);
            if (info.Frequency <= 0 || info.Channels <= 0)
            {
                throw CreateBassException("BASS returned invalid stream format.");
            }

            var waveFormat = new NAudioWaveFormat(info.Frequency, 16, info.Channels);
            long channelLength = Bass.ChannelGetLength(stream, PositionFlags.Bytes);
            int initialCapacity = channelLength is > 0 and <= int.MaxValue
                ? (int)channelLength
                : DecodeBufferBytes;

            using var decoded = new MemoryStream(initialCapacity);
            var nativeBuffer = Marshal.AllocHGlobal(DecodeBufferBytes);
            var managedBuffer = ArrayPool<byte>.Shared.Rent(DecodeBufferBytes);

            try
            {
                while (true)
                {
                    int bytesRead = Bass.ChannelGetData(stream, nativeBuffer, DecodeBufferBytes);

                    if (bytesRead > 0)
                    {
                        Marshal.Copy(nativeBuffer, managedBuffer, 0, bytesRead);
                        decoded.Write(managedBuffer, 0, bytesRead);
                        continue;
                    }

                    if (bytesRead == 0 || Bass.LastError == Errors.Ended)
                        break;

                    throw CreateBassException("Failed to decode BASS stream.");
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(managedBuffer);
                Marshal.FreeHGlobal(nativeBuffer);
            }

            return new DecodedPcmAudio(decoded.ToArray(), waveFormat);
        }
        finally
        {
            Bass.StreamFree(stream);
        }
    }

    private static void EnsureInitialized()
    {
        lock (s_initLock)
        {
            if (s_initialized)
                return;

            if (s_initializationAttempted)
            {
                throw new InvalidOperationException("BASS failed to initialize.", s_initializationException);
            }

            s_initializationAttempted = true;

            try
            {
                if (!Bass.Configure((Configuration)BassConfigMp3OldGaps, 1))
                {
                    throw CreateBassException("Failed to enable BASS_CONFIG_MP3_OLDGAPS.");
                }

                if (!Bass.Init(Bass.NoSoundDevice, 44100, DeviceInitFlags.Default, IntPtr.Zero, IntPtr.Zero) &&
                    Bass.LastError != Errors.Already)
                {
                    throw CreateBassException("Failed to initialize BASS no-sound device.");
                }

                Bass.CurrentDevice = Bass.NoSoundDevice;
                s_initialized = true;
            }
            catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or InvalidOperationException)
            {
                s_initializationException = ex;
                throw new InvalidOperationException("BASS failed to initialize.", ex);
            }
        }
    }

    private static InvalidOperationException CreateBassException(string message)
        => new($"{message} LastError={Bass.LastError}.");
}

internal sealed record DecodedPcmAudio(byte[] Data, NAudioWaveFormat WaveFormat);
