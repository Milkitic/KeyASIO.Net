namespace KeyAsio.Shared.OsuMemory;

public record OsuMemoryData
{
    // Beatmap Data
    //public int Id { get; set; }
    //public int SetId { get; set; }
    //public string MapString { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string OsuFileName { get; set; } = string.Empty;
    //public string MD5 { get; set; } = string.Empty;
    //public float AR { get; set; }
    //public float CS { get; set; }
    //public float HP { get; set; }
    //public float OD { get; set; }
    //public short Status { get; set; } // Beatmap Status

    // General Data
    public int RawStatus { get; set; } // OsuStatus
    //public int GameMode { get; set; }
    //public int Retries { get; set; }
    //public double TotalAudioTime { get; set; }
    //public bool ChatIsExpanded { get; set; }
    public int Mods { get; set; }
    //public bool ShowPlayingInterface { get; set; }
    //public string OsuVersion { get; set; } = string.Empty;

    // Additional Data
    //public bool IsLoggedIn { get; set; }
    public string Username { get; set; } = string.Empty;
    //public string SkinFolder { get; set; } = string.Empty;
    public bool IsReplay { get; set; }
    public int Score { get; set; }
    public ushort Combo { get; set; }
}
