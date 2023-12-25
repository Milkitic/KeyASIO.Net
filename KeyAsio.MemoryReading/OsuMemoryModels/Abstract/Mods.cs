using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Abstract
{
    public class Mods
    {
        [MemoryAddress("[+0x1C]+0x8")]
        public int ModsXor1 { get; set; }
        [MemoryAddress("[+0x1C]+0xC")]
        public int ModsXor2 { get; set; }
        public int Value => ModsXor1 ^ ModsXor2;
    }
}