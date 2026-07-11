using KeyAsio.LazerProtocol;
using KeyAsio.Plugins.Contracts;

namespace KeyAsio.Sync.Sources;

public sealed class LazerIpcFrame
{
    public int Version { get; private set; }
    public int ProcessId { get; private set; }
    public int Status { get; private set; }
    public int PlayTime { get; private set; }
    public uint Mods { get; private set; }
    public int Combo { get; private set; }
    public int Score { get; private set; }
    public bool IsReplay { get; private set; }
    public string? Username { get; private set; }
    public string? BeatmapFolder { get; private set; }
    public string? BeatmapFilename { get; private set; }
    public LazerFile[] BeatmapFiles { get; private set; } = [];
    public SyncStatistics Statistics { get; private set; }
    public int HitErrorIndex { get; private set; }
    public int[] HitErrors { get; private set; } = [];
    public LazerSkinInfo[]? SkinInfos { get; private set; }
    public string? UserDataDirectory { get; private set; }
    public string? ExeDirectory { get; private set; }

    public void Reset()
    {
        Version = 0;
        ProcessId = 0;
        Status = 0;
        PlayTime = 0;
        Mods = 0;
        Combo = 0;
        Score = 0;
        IsReplay = false;
        Username = null;
        BeatmapFolder = null;
        BeatmapFilename = null;
        BeatmapFiles = [];
        Statistics = SyncStatistics.Empty;
        HitErrorIndex = 0;
        HitErrors = [];
        SkinInfos = null;
        UserDataDirectory = null;
        ExeDirectory = null;
    }

    public void ClearBeatmapFiles()
    {
        BeatmapFiles = [];
    }

    public bool HasLazerSkinInfos => SkinInfos != null;

    public void Apply(LazerDeltaFrame deltaFrame)
    {
        Version = deltaFrame.Version;

        foreach (var field in deltaFrame.Fields)
        {
            switch (field.Kind)
            {
                case LazerFieldKind.ProcessId:
                    ProcessId = field.IntValue;
                    break;

                case LazerFieldKind.Status:
                    Status = field.IntValue;
                    break;

                case LazerFieldKind.PlayTime:
                    PlayTime = field.IntValue;
                    break;

                case LazerFieldKind.Mods:
                    Mods = field.UIntValue;
                    break;

                case LazerFieldKind.Combo:
                    Combo = field.IntValue;
                    break;

                case LazerFieldKind.Score:
                    Score = field.IntValue;
                    break;

                case LazerFieldKind.IsReplay:
                    IsReplay = field.BoolValue;
                    break;

                case LazerFieldKind.Username:
                    Username = field.StringValue;
                    break;

                case LazerFieldKind.BeatmapFolder:
                    BeatmapFolder = field.StringValue;
                    break;

                case LazerFieldKind.BeatmapFilename:
                    BeatmapFilename = field.StringValue;
                    break;

                case LazerFieldKind.BeatmapFiles:
                    BeatmapFiles = field.FilesValue ?? [];
                    break;

                case LazerFieldKind.Statistics:
                    Statistics = field.StatisticsValue.ToSyncStatistics();
                    break;

                case LazerFieldKind.HitErrors:
                    HitErrorIndex = field.IntValue;
                    HitErrors = field.IntArrayValue ?? [];
                    break;

                case LazerFieldKind.SkinInfos:
                    SkinInfos = field.SkinInfosValue;
                    break;

                case LazerFieldKind.UserDataDirectory:
                    UserDataDirectory = field.StringValue;
                    break;

                case LazerFieldKind.ExeDirectory:
                    ExeDirectory = field.StringValue;
                    break;
            }
        }
    }
}

internal static class LazerStatisticsExtensions
{
    public static SyncStatistics ToSyncStatistics(this LazerStatistics statistics)
        => new(statistics.Perfect, statistics.Great, statistics.Good, statistics.Ok, statistics.Meh, statistics.Miss);
}