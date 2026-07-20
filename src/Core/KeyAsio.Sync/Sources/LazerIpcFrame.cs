using OverlayAPI.LazerProtocol;

namespace KeyAsio.Sync.Sources;

public sealed class LazerIpcFrame
{
    public int ProcessId { get; private set; }
    public int Status { get; private set; }
    public int PlayTime { get; private set; }
    public double BeatmapOffset { get; private set; }
    public uint Mods { get; private set; }
    public int Combo { get; private set; }
    public bool IsReplay { get; private set; }
    public string? BeatmapFilename { get; private set; }
    public LazerFile[] BeatmapFiles { get; private set; } = [];
    public LazerSkinInfo[]? SkinInfos { get; private set; }

    public void Reset()
    {
        ProcessId = 0;
        Status = 0;
        PlayTime = 0;
        BeatmapOffset = 0;
        Mods = 0;
        Combo = 0;
        IsReplay = false;
        BeatmapFilename = null;
        BeatmapFiles = [];
        SkinInfos = null;
    }

    public void ClearBeatmapFiles()
    {
        BeatmapFiles = [];
    }

    public void Apply(LazerDeltaFrame deltaFrame)
    {
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

                case LazerFieldKind.BeatmapOffset:
                    BeatmapOffset = double.IsFinite(field.DoubleValue) ? field.DoubleValue : 0;
                    break;

                case LazerFieldKind.Mods:
                    Mods = field.UIntValue;
                    break;

                case LazerFieldKind.Combo:
                    Combo = field.IntValue;
                    break;

                case LazerFieldKind.IsReplay:
                    IsReplay = field.BoolValue;
                    break;

                case LazerFieldKind.BeatmapFilename:
                    BeatmapFilename = field.StringValue;
                    break;

                case LazerFieldKind.BeatmapFiles:
                    BeatmapFiles = field.FilesValue ?? [];
                    break;

                case LazerFieldKind.SkinInfos:
                    SkinInfos = field.SkinInfosValue;
                    break;
            }
        }
    }
}
