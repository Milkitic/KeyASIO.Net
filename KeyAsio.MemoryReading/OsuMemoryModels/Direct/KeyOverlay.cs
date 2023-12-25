using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Direct
{
    [MemoryAddress(KeyOverlay.ClassAddress, false, true, nameof(RawHasKeyOverlay))]
    public class KeyOverlay
    {
        internal const string ClassAddress = "[[[[CurrentRuleset]+0xB0]+0x10]+0x4]";

        [MemoryAddress("")]
        private int? RawHasKeyOverlay { get; set; }
        public bool Enabled => RawHasKeyOverlay.HasValue && RawHasKeyOverlay != 0;

        [MemoryAddress("[+0x8]+0x1C")]
        public bool K1Pressed { get; set; }
        [MemoryAddress("[+0x8]+0x14")]
        public int K1Count { get; set; }
        [MemoryAddress("[+0xC] + 0x1C")]
        public bool K2Pressed { get; set; }
        [MemoryAddress("[+0xC] + 0x14")]
        public int K2Count { get; set; }
        [MemoryAddress("[+0x10] + 0x1C")]
        public bool M1Pressed { get; set; }
        [MemoryAddress("[+0x10] + 0x14")]
        public int M1Count { get; set; }
        [MemoryAddress("[+0x14] + 0x1C")]
        public bool M2Pressed { get; set; }
        [MemoryAddress("[+0x14] + 0x14")]
        public int M2Count { get; set; }
    }
}