using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Direct
{
    [MemoryAddress(null)]
    public class BanchoUser
    {
        [MemoryAddress("IsLoggedIn")]
        public bool IsLoggedIn { get; set; }

        internal InternalBanchoUser internalBanchoUser = new();

        [MemoryAddress(null)]
        internal InternalBanchoUser InternalBanchoUser
        {
            get
            {
                return IsLoggedIn
                    ? internalBanchoUser
                    : null;
            }
            set => internalBanchoUser = value;
        }

        public string Username => InternalBanchoUser?.Username;
        //public int? UserId => InternalBanchoUser?.UserId;
        //public string UserCountry => InternalBanchoUser?.UserCountry;
        //public string UserPpAccLevel  => InternalBanchoUser?.UserPpAccLevel;
        //public BanchoStatus? BanchoStatus => (BanchoStatus?)InternalBanchoUser?.RawBanchoStatus;
    }

    [MemoryAddress("[UserPanel]", checkClassAddress: true)]
    internal class InternalBanchoUser
    {
        [MemoryAddress("+0x30")] public string Username { get; set; }
        //[MemoryAddress("+0x70")] public int? UserId { get; set; }
        //[MemoryAddress("+0x2C")] public string UserCountry { get; set; }
        //[MemoryAddress("+0x1C")] public string UserPpAccLevel { get; set; }
        ////[MemoryAddress("+0x74")] public float? UserLevel { get; set; }
        //[MemoryAddress("+0x88")] public int? RawBanchoStatus { get; set; }
    }
}
