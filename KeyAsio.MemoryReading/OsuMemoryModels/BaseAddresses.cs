using KeyAsio.MemoryReading.OsuMemoryModels.Direct;

namespace KeyAsio.MemoryReading.OsuMemoryModels
{
    public class OsuBaseAddresses
    {
        public CurrentBeatmap Beatmap { get; set; } = new CurrentBeatmap();
        public Player Player { get; set; } = new Player();
        //public LeaderBoard LeaderBoard { get; set; } = new LeaderBoard();
        //public SongSelectionScores SongSelectionScores { get; set; } = new SongSelectionScores();
        public Skin Skin { get; set; } = new Skin();
        //public ResultsScreen ResultsScreen { get; set; } = new ResultsScreen();
        public GeneralData GeneralData { get; set; } = new GeneralData();
        public BanchoUser BanchoUser { get; set; } = new BanchoUser();
        //public KeyOverlay KeyOverlay { get; set; } = new KeyOverlay();
    }
}