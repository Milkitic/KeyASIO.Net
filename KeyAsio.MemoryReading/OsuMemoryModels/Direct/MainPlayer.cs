using KeyAsio.MemoryReading.OsuMemoryModels.Abstract;
using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Direct
{
    [MemoryAddress("[[[[CurrentRuleset]+0x7C]+0x24]+0x10]")]
    public class MainPlayer : MultiplayerPlayer
    {
        [MemoryAddress("[+0x24]+0x20")]
        public bool IsLeaderboardVisible { get; set; }
    }
}