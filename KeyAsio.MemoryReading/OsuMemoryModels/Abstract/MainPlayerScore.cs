using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Abstract
{
    public class MainPlayerScore : PlayerScore
    {
        [MemoryAddress("+0x6C")] public int Position { get; set; }
    }
}