using OsuMemoryDataProvider;
using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Direct
{
    public class GeneralData
    {
        [MemoryAddress("OsuStatus")] public int RawStatus { get; set; }
        //[MemoryAddress("GameMode")] public int GameMode { get; set; }
        //[MemoryAddress("Retries")] public int Retries { get; set; }
        //[MemoryAddress("AudioTime")] public int AudioTime { get; set; }
        //[MemoryAddress("[TotalAudioTimeBase]+0x4")] public double TotalAudioTime { get; set; }
        //[MemoryAddress("ChatIsExpanded", true)] public bool ChatIsExpanded { get; set; }
        [MemoryAddress("Mods")] public int Mods { get; set; }
        //[MemoryAddress("[Settings + 0x4] + 0xC")] public bool ShowPlayingInterface { get; set; }
        [MemoryAddress("[Settings + 0x44] + 0x4")] public string OsuVersion { get; set; }
        
        public OsuMemoryStatus OsuStatus
        {
            get
            {
                if (Enum.IsDefined(typeof(OsuMemoryStatus), RawStatus))
                {
                    return (OsuMemoryStatus)RawStatus;
                }

                return OsuMemoryStatus.Unknown;
            }
        }
    }

    public class GeneralDataSlim
    {
        [MemoryAddress("AudioTime")] public int AudioTime { get; set; }
    }
}