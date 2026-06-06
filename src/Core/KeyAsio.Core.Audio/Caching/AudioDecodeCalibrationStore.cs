using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Core.Audio.Caching;

public sealed class AudioDecodeCalibrationStore : IAudioDecodeCalibrationProvider
{
    public const string EnvironmentVariable = "OSUPLAYER_AUDIO_DECODE_CALIBRATIONS";
    public const string DefaultFileName = "audio-decode-calibrations.json";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly Dictionary<string, AudioDecodeCalibration> _calibrations;

    private AudioDecodeCalibrationStore(Dictionary<string, AudioDecodeCalibration> calibrations)
    {
        _calibrations = calibrations;
    }

    public static AudioDecodeCalibrationStore Empty { get; } = new(new Dictionary<string, AudioDecodeCalibration>());

    public int Count => _calibrations.Count;

    public bool TryGetCalibration(string sourceHash, out AudioDecodeCalibration calibration)
    {
        return _calibrations.TryGetValue(sourceHash, out calibration!);
    }

    public static AudioDecodeCalibrationStore FromCalibrations(IEnumerable<AudioDecodeCalibration> calibrations)
    {
        var byHash = new Dictionary<string, AudioDecodeCalibration>(StringComparer.Ordinal);
        foreach (var calibration in calibrations)
        {
            if (string.IsNullOrWhiteSpace(calibration.SourceHash))
                continue;

            byHash[calibration.SourceHash] = calibration;
        }

        return byHash.Count == 0 ? Empty : new AudioDecodeCalibrationStore(byHash);
    }

    public static AudioDecodeCalibrationStore Load(string path)
    {
        var filePath = ResolveFilePath(path);
        using var stream = File.OpenRead(filePath);
        var manifest = JsonSerializer.Deserialize<AudioDecodeCalibrationManifest>(stream, s_jsonOptions) ??
                       new AudioDecodeCalibrationManifest();

        return FromCalibrations(manifest.Calibrations);
    }

    public static IAudioDecodeCalibrationProvider? TryLoadFromEnvironment(ILogger? logger = null)
    {
        var path = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var store = Load(path);
            logger?.LogInformation("Loaded {Count} audio decode calibrations from {Path}", store.Count, path);
            return store.Count == 0 ? null : store;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to load audio decode calibrations from {Path}", path);
            return null;
        }
    }

    public static void Save(string path, IEnumerable<AudioDecodeCalibration> calibrations)
    {
        var filePath = Path.GetExtension(path).Length == 0 ? Path.Combine(path, DefaultFileName) : path;
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var manifest = new AudioDecodeCalibrationManifest
        {
            Calibrations = calibrations
                .Where(static calibration => !string.IsNullOrWhiteSpace(calibration.SourceHash))
                .OrderBy(static calibration => calibration.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static calibration => calibration.SourceHash, StringComparer.Ordinal)
                .ToList()
        };

        var json = JsonSerializer.Serialize(manifest, s_jsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static string ResolveFilePath(string path)
    {
        if (Directory.Exists(path))
            return Path.Combine(path, DefaultFileName);

        return path;
    }

    private sealed class AudioDecodeCalibrationManifest
    {
        public int Version { get; init; } = 1;

        public List<AudioDecodeCalibration> Calibrations { get; init; } = [];
    }
}
