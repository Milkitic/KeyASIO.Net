using System.Text;

namespace KeyAsio.Audio.SoundTouch;

internal class SoundTouch : IDisposable
{
    static SoundTouch()
    {
        var is64Bit = Environment.Is64BitProcess;
        var libDir = Path.Combine(Configuration.Instance.SoundTouchDir);
        var os = Environment.OSVersion;
        if (os.Platform != PlatformID.Win32NT)
        {
            throw new PlatformNotSupportedException("Platform not support for SoundTouch: " + os.VersionString);
        }

        AddEnvironmentPaths(libDir, is64Bit
            ? Path.Combine(libDir, "win-x64", "SoundTouch")
            : Path.Combine(libDir, "win-x86", "SoundTouch")
        );
    }

    private IntPtr _handle;
    private string? _versionString;
    public SoundTouch()
    {
        _handle = SoundTouchInterop.soundtouch_createInstance();
    }

    public string VersionString
    {
        get
        {
            if (_versionString == null)
            {
                var s = new StringBuilder(100);

                SoundTouchInterop.soundtouch_getVersionString2(s, s.Capacity);
                _versionString = s.ToString();
            }

            return _versionString;
        }
    }

    public void SetPitchOctaves(float pitchOctaves)
    {
        SoundTouchInterop.soundtouch_setPitchOctaves(_handle, pitchOctaves);
    }

    public void SetSampleRate(int sampleRate)
    {
        SoundTouchInterop.soundtouch_setSampleRate(_handle, (uint)sampleRate);
    }

    public void SetChannels(int channels)
    {
        SoundTouchInterop.soundtouch_setChannels(_handle, (uint)channels);
    }

    private void DestroyInstance()
    {
        if (_handle != IntPtr.Zero)
        {
            SoundTouchInterop.soundtouch_destroyInstance(_handle);
            _handle = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        DestroyInstance();
        GC.SuppressFinalize(this);
    }

    ~SoundTouch()
    {
        DestroyInstance();
    }

    public void PutSamples(float[] samples, int numSamples)
    {
        SoundTouchInterop.soundtouch_putSamples(_handle, samples, numSamples);
    }

    public int ReceiveSamples(float[] outBuffer, int maxSamples)
    {
        return (int)SoundTouchInterop.soundtouch_receiveSamples(_handle, outBuffer, (uint)maxSamples);
    }

    public bool IsEmpty => SoundTouchInterop.soundtouch_isEmpty(_handle) != 0;

    public int NumberOfSamplesAvailable => (int)SoundTouchInterop.soundtouch_numSamples(_handle);

    public int NumberOfUnprocessedSamples => SoundTouchInterop.soundtouch_numUnprocessedSamples(_handle);

    public void Flush()
    {
        SoundTouchInterop.soundtouch_flush(_handle);
    }

    public void Clear()
    {
        SoundTouchInterop.soundtouch_clear(_handle);
    }

    public void SetRate(float newRate)
    {
        SoundTouchInterop.soundtouch_setRate(_handle, newRate);
    }

    public void SetTempo(float newTempo)
    {
        SoundTouchInterop.soundtouch_setTempo(_handle, newTempo);
    }

    public int GetUseAntiAliasing()
    {
        return SoundTouchInterop.soundtouch_getSetting(_handle, SoundTouchSettings.UseAaFilter);
    }

    public void SetUseAntiAliasing(bool useAntiAliasing)
    {
        SoundTouchInterop.soundtouch_setSetting(_handle, SoundTouchSettings.UseAaFilter, useAntiAliasing ? 1 : 0);
    }

    public void SetUseQuickSeek(bool useQuickSeek)
    {
        SoundTouchInterop.soundtouch_setSetting(_handle, SoundTouchSettings.UseQuickSeek, useQuickSeek ? 1 : 0);
    }

    public int GetUseQuickSeek()
    {
        return SoundTouchInterop.soundtouch_getSetting(_handle, SoundTouchSettings.UseQuickSeek);
    }

    private static void AddEnvironmentPaths(params string[] paths)
    {
        var path = new[] { Environment.GetEnvironmentVariable("PATH") ?? string.Empty };

        string newPath = string.Join(Path.PathSeparator.ToString(), path.Concat(paths));

        Environment.SetEnvironmentVariable("PATH", newPath);
    }
}