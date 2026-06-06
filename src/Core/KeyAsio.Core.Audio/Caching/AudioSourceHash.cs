namespace KeyAsio.Core.Audio.Caching;

public static class AudioSourceHash
{
    public static string Compute(ReadOnlySpan<byte> data)
    {
        return Blake3.Hasher.Hash(data).ToString();
    }

    public static async Task<string> ComputeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var data = await File.ReadAllBytesAsync(filePath, cancellationToken);
        return Compute(data);
    }
}
