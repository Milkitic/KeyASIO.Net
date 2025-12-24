using KeyAsio.Core.Audio.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace AudioTests;

using NAudio.Wave;

public class UnitTest1
{
    [Fact]
    public async Task TestAudioFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<AudioCacheManager>();
        var provider = services.BuildServiceProvider();

        var audioFactory = provider.GetRequiredService<AudioCacheManager>();
        var cacheResult = await audioFactory.GetOrCreateOrEmptyFromFileAsync("files/normal-hitnormal.wav", new WaveFormat(48000, 2));
    }

    [Fact]
    public void TestSnare()
    {
        var snare = new SnareDrumOneShotProvider(new WaveFormat(44100, 2));
        //snare.Trigger();

        var outputFile = "snare_test.wav";
        // Ensure we are writing to a place we can find or just current directory
        if (File.Exists(outputFile)) File.Delete(outputFile);

        // Run for 2 seconds
        var duration = TimeSpan.FromSeconds(2);
        var buffer = new float[snare.WaveFormat.SampleRate * snare.WaveFormat.Channels]; // 1 second buffer
        long totalBytes = (long)(duration.TotalSeconds * snare.WaveFormat.AverageBytesPerSecond);

        using var writer = new WaveFileWriter(outputFile, snare.WaveFormat);

        while (writer.Length < totalBytes)
        {
            int samplesRead = snare.Read(buffer, 0, buffer.Length);
            
            // SnareDrumOneShotProvider returns 0 (or partial) when finished, but zeroes the rest of the buffer.
            // We force writing the full buffer to ensure the output file has the desired duration (silence padding).
            writer.WriteSamples(buffer, 0, buffer.Length);

            if (writer.Length >= totalBytes) break;
        }

        Assert.True(File.Exists(outputFile));
        Assert.True(new FileInfo(outputFile).Length > 44);
    }
}