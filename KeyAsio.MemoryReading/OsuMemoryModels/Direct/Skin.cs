using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Direct
{
    [MemoryAddress("[CurrentSkinData]")]
    public class Skin
    {
        [MemoryAddress("+0x44")]
        public string Folder { get; set; }
    }
}