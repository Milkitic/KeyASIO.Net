using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Abstract
{
    public class RulesetPlayData
    {
        //[MemoryAddress("[+0x38]+0x28")]
        //public string Username { get; set; }

        [MemoryAddress("[+0x38]")]
        public Mods Mods { get; set; } = new Mods();

        [MemoryAddress("[+0x38]+0x64")]
        public int Mode { get; set; }
        //[MemoryAddress("[+0x38]+0x68")]
        //public ushort MaxCombo { get; set; }
        [MemoryAddress("[CurrentRuleset]+0x100")]
        public virtual int Score { get; set; }
        //public virtual int ScoreV2 { get => Score; set => Score = value; }
        //[MemoryAddress("[+0x38]+0x88")]
        //public ushort Hit100 { get; set; }
        //[MemoryAddress("[+0x38]+0x8A")]
        //public ushort Hit300 { get; set; }
        //[MemoryAddress("[+0x38]+0x8C")]
        //public ushort Hit50 { get; set; }
        //[MemoryAddress("[+0x38]+0x8E")]
        //public ushort HitGeki { get; set; }
        //[MemoryAddress("[+0x38]+0x90")]
        //public ushort HitKatu { get; set; }
        //[MemoryAddress("[+0x38]+0x92")]
        //public ushort HitMiss { get; set; }
        [MemoryAddress("[+0x38]+0x94")]
        public ushort Combo { get; set; }

    }
}