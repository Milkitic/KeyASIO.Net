using KeyAsio.MemoryReading.OsuMemoryModels.Abstract;
using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Direct
{
    [MemoryAddress("[[CurrentRuleset]+0x7C]+0x24", false, true, nameof(RawHasLeaderboard))]
    public class LeaderBoard
    {
        //single player: top50 + player top score + current score. possibly more with local scores leaderboard?
        protected const int AmountOfPlayerSlots = 52;

        public LeaderBoard()
        {
            RawPlayers = Enumerable.Range(0, AmountOfPlayerSlots).Select(x => new MultiplayerPlayer()).ToList();
        }

        [MemoryAddress("")]
        private int? RawHasLeaderboard { get; set; }
        public bool HasLeaderBoard => RawHasLeaderboard.HasValue && RawHasLeaderboard != 0;
        private MainPlayer _mainPlayer = new MainPlayer();

        [MemoryAddress("[[]+0x10]")]
        public MainPlayer MainPlayer
        {
            get => HasLeaderBoard ? _mainPlayer : null;
            set => _mainPlayer = value;
        }

        private int? _amountOfPlayers;

        [MemoryAddress("[[]+0x4]+0xC")]
        public int? AmountOfPlayers
        {
            get => _amountOfPlayers;
            set
            {
                _amountOfPlayers = value;
                if (value.HasValue && value.Value > 0)
                    Players = _players.GetRange(0, Math.Clamp(value.Value, 0, AmountOfPlayerSlots));
                else
                    Players.Clear();
            }
        }
        private List<MultiplayerPlayer> _players;
        [MemoryAddress("[]+0x4")]
        private List<MultiplayerPlayer> RawPlayers
        {
            //toggle reading of players depending on HasLeaderboard value
            get => HasLeaderBoard ? _players : null;
            set
            {
                _players = value;
            }
        }
        public List<MultiplayerPlayer> Players { get; private set; } = new List<MultiplayerPlayer>();
    }
}