namespace KeyAsio.Plugins.Abstractions.OsuMemory;

public enum OsuMemoryStatus
{
    Unknown = -0xff,
    NotRunning = -0x0f,

    MainView = 0,
    Editing = 1,
    Playing = 2,
    GameShutdown = 3,
    EditSongSelection = 4,
    SongSelection = 5,
    ResultsScreen = 7,
    GameStartup = 10,
    MultiLobby = 11,
    MultiRoom = 12,
    MultiSongSelection = 13,
    MultiResultsScreen = 14,
    OsuDirect = 15,
    TagCoopRanking = 17,
    TeamRanking = 18,
    BeatmapProcessing = 19,
    Tourney = 22,
}