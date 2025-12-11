namespace KeyAsio.Memory.Samples;

internal class SampleOsuData
{
    // Beatmap Data
    public int Id { get; set; }
    public int SetId { get; set; }
    public string MapString { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string OsuFileName { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public float AR { get; set; }
    public float CS { get; set; }
    public float HP { get; set; }
    public float OD { get; set; }
    public short Status { get; set; }

    // General Data
    public int RawStatus { get; set; }
    public int GameMode { get; set; }
    public int Retries { get; set; }
    public int AudioTime { get; set; }
    public double TotalAudioTime { get; set; }
    public bool ChatIsExpanded { get; set; }
    public int Mods { get; set; }
    public bool ShowPlayingInterface { get; set; }
    public string OsuVersion { get; set; } = string.Empty;

    // Additional Data
    public bool IsLoggedIn { get; set; }
    public string Username { get; set; } = string.Empty;
    public string SkinFolder { get; set; } = string.Empty;
    public bool IsReplay { get; set; }
    public int Score { get; set; }
    public ushort Combo { get; set; }

    public override string ToString()
    {
        return $"ID: {Id}, SetID: {SetId}, Map: {MapString}, AR: {AR}, CS: {CS}, AudioTime: {AudioTime}";
    }
}