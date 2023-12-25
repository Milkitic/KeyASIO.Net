using KeyAsio.MemoryReading.OsuMemoryModels.Abstract;
using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Direct
{
    [MemoryAddress("[CurrentRuleset]")]
    public class ResultsScreen : RulesetPlayData
    {
        [MemoryAddress("[+0x38]+0x78")]
        public override int Score { get; set; }
    }
}