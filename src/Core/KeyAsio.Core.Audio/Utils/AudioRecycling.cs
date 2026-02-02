using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;
using Milki.Extensions.Threading;
using NAudio.Wave;

namespace KeyAsio.Core.Audio.Utils;

public static class AudioRecycling
{
    private static readonly SingleSynchronizationContext s_recyclerContext =
        new(name: "Audio Recycler Thread", staThread: false, threadPriority: ThreadPriority.BelowNormal);

    private static readonly SendOrPostCallback s_recycleChainCallback =
        state =>
        {
            if (state is ISampleProvider provider)
            {
                RecycleSourceChain(provider);
            }
        };

    public static void QueueForRecycle(ISampleProvider provider)
    {
        s_recyclerContext.Post(s_recycleChainCallback, provider);
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
                case CachedAudioProvider cache:
                    RecyclableSampleProviderFactory.Return(cache);
                    break;
            }

            current = next;
        }
    }

    public static void Shutdown()
    {
        s_recyclerContext.Dispose();
    }
}