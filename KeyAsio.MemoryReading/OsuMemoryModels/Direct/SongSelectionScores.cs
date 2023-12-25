using KeyAsio.MemoryReading.OsuMemoryModels.Abstract;
using ProcessMemoryDataFinder.Structured;

namespace KeyAsio.MemoryReading.OsuMemoryModels.Direct
{
    [MemoryAddress("[CurrentBeatmap]", false, true, nameof(RawHasScores))]
    public class SongSelectionScores
    {
        //single player: top50 + player top score + current score.
        protected const int AmountOfPlayerSlots = 52;
        public SongSelectionScores()
        {
            RawScores = Enumerable.Range(0, AmountOfPlayerSlots).Select(x => new PlayerScore()).ToList();
        }

        [MemoryAddress("+0x104")]
        private int RawRankingType { get; set; }
        public RankingType RankingType =>
            Enum.IsDefined(typeof(RankingType), RawRankingType)
                ? (RankingType)RawRankingType
                : RankingType.Unknown;
        [MemoryAddress("+0xD0")]
        public int TotalScores { get; set; }

        [MemoryAddress("+0x98")]
        private int? RawHasMainPlayerScore { get; set; }
        private MainPlayerScore _mainPlayerScore = new MainPlayerScore();
        [MemoryAddress("[+0x98]")]
        public MainPlayerScore MainPlayerScore
        {
            get => RawHasMainPlayerScore.HasValue && RawHasMainPlayerScore != 0 ? _mainPlayerScore : null;
            set => _mainPlayerScore = value;
        }

        private int? _amountOfScores;
        [MemoryAddress("[+0x9C]+0xC")]
        public int? AmountOfScores
        {
            get => _amountOfScores;
            set
            {
                _amountOfScores = value;
                if (value.HasValue && value.Value > 0)
                    Scores = _scores.GetRange(0, Math.Clamp(value.Value, 0, AmountOfPlayerSlots));
                else
                    Scores.Clear();
            }
        }

        [MemoryAddress("+0x9C")]
        private int? RawHasScores { get; set; }
        private List<PlayerScore> _scores;
        [MemoryAddress("+0x9C")]
        private List<PlayerScore> RawScores
        {
            //toggle reading of players
            get => RawHasScores.HasValue && RawHasScores != 0 ? _scores : null;
            set
            {
                _scores = value;
            }
        }
        public List<PlayerScore> Scores { get; private set; } = new List<PlayerScore>();

    }
}