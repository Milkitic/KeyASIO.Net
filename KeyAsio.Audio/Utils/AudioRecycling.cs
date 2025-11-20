using KeyAsio.Audio.SampleProviders;
using KeyAsio.Audio.SampleProviders.BalancePans;
using Milki.Extensions.Threading;
using NAudio.Wave;

namespace KeyAsio.Audio.Utils;

public static class AudioRecycling
{
    private static readonly SingleSynchronizationContext RecyclerContext =
        new(name: "Audio Recycler Thread", staThread: false, threadPriority: ThreadPriority.BelowNormal);

    private static readonly SendOrPostCallback RecycleChainCallback =
        state =>
        {
            if (state is ISampleProvider provider)
            {
                RecycleSourceChain(provider);
            }
        };

    public static void QueueForRecycle(ISampleProvider provider)
    {
        RecyclerContext.Post(RecycleChainCallback, provider);
    }

    private static void RecycleSourceChain(ISampleProvider provider)
    {
        var current = provider;

        while (current is IRecyclableProvider recyclable)
        {
            var next = recyclable.ResetAndGetSource();
            switch (current)
            {
                case EnhancedVolumeSampleProvider vol:
                    RecyclableSampleProviderFactory.Return(vol);
                    break;
                case ProfessionalBalanceProvider pan:
                    RecyclableSampleProviderFactory.Return(pan);
                    break;
                case LoopSampleProvider loop:
                    RecyclableSampleProviderFactory.Return(loop);
                    break;
                case SeekableCachedAudioProvider cache:
                    RecyclableSampleProviderFactory.Return(cache);
                    break;
            }

            current = next;
        }
    }

    public static void Shutdown()
    {
        RecyclerContext.Dispose();
    }
}