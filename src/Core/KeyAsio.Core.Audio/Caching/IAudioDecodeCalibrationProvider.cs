namespace KeyAsio.Core.Audio.Caching;

public interface IAudioDecodeCalibrationProvider
{
    bool TryGetCalibration(string sourceHash, out AudioDecodeCalibration calibration);
}
