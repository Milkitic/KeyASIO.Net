using KeyAsio.Core.Audio.Caching;
using Microsoft.Extensions.DependencyInjection;
using NAudio.Wave;

namespace AudioTests
{
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
    }
}