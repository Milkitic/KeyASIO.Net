using KeyAsio.MemoryReading.OsuMemoryModels.Abstract;
using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Direct
{
    [MemoryAddress("[[CurrentRuleset] + 0x68]")]
    public class Player : RulesetPlayData
    {
        //[MemoryAddress("")]
        //public int Retries { get; set; }

        //[MemoryAddress("[+0x40]+0x14")]
        //public double HPSmooth { get; set; }
        //[MemoryAddress("[+0x40]+0x1C")]
        //public double HP { get; set; }
        //[MemoryAddress("[+0x48]+0xC")]
        //public double Accuracy { get; set; }
        //[MemoryAddress("[+0x38]+0x38")]
        //public List<int> HitErrors { get; set; }

        [MemoryAddress("IsReplay")]
        public bool IsReplay { get; set; }
    }
}