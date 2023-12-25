using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Abstract
{
    public class MultiplayerPlayerPlayData
    {
        [MemoryAddress("+0x94")]
        public ushort Combo { get; set; }
        [MemoryAddress("+0x68")]
        public ushort MaxCombo { get; set; }
        [MemoryAddress("")]
        public Mods Mods { get; set; } = new Mods();
        [MemoryAddress("+0x8A")]
        public ushort Hit300 { get; set; }
        [MemoryAddress("+0x88")]
        public ushort Hit100 { get; set; }
        [MemoryAddress("+0x8C")]
        public ushort Hit50 { get; set; }
        [MemoryAddress("+0x92")]
        public ushort HitMiss { get; set; }
    }
}