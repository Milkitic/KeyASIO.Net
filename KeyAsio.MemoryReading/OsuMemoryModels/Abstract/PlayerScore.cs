using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Abstract
{
    public class PlayerScore
    {
        [MemoryAddress("+0x28")] public string Username { get; set; }
        [MemoryAddress("")] public Mods Mods { get; set; } = new Mods();
        [MemoryAddress("+0x64")] public int Mode { get; set; }
        [MemoryAddress("+0x68")] public ushort MaxCombo { get; set; }
        [MemoryAddress("+0x78")] public virtual int Score { get; set; }
        [MemoryAddress("+0x88")] public ushort Hit100 { get; set; }
        [MemoryAddress("+0x8A")] public ushort Hit300 { get; set; }
        [MemoryAddress("+0x8C")] public ushort Hit50 { get; set; }
        [MemoryAddress("+0x8E")] public ushort HitGeki { get; set; }
        [MemoryAddress("+0x90")] public ushort HitKatu { get; set; }
        [MemoryAddress("+0x92")] public ushort HitMiss { get; set; }
        [MemoryAddress("+0xA0")] protected long RawDate { get; set; }
        public DateTime Date
        {
            get
            {
                var ticks = RawDate & InternalTicksMask;
                if (ticks > DateTime.MinValue.Ticks && ticks < DateTime.MaxValue.Ticks)
                    return new DateTime(ticks);
                return DateTime.MinValue;
            }
        }
        [MemoryAddress("[+0x48]+0x70")] public int? UserId { get; set; }

        private static long InternalTicksMask = 4611686018427387903L;
    }
}