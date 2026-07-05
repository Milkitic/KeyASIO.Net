using System.Buffers.Binary;
using System.Text;
using KeyAsio.Plugins.Abstractions;

namespace KeyAsio.Shared.OsuMemory;

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
    public LazerIpcFile[] BeatmapFiles { get; private set; } = [];
    public SyncStatistics Statistics { get; private set; }
    public int HitErrorIndex { get; private set; }
    public int[] HitErrors { get; private set; } = [];

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
    }

    public void ClearBeatmapFiles()
    {
        BeatmapFiles = [];
    }

    public void Apply(LazerIpcDeltaFrame deltaFrame)
    {
        Version = deltaFrame.Version;

        foreach (var field in deltaFrame.Fields)
        {
            switch (field.Kind)
            {
                case LazerIpcFieldKind.ProcessId:
                    ProcessId = field.IntValue;
                    break;

                case LazerIpcFieldKind.Status:
                    Status = field.IntValue;
                    break;

                case LazerIpcFieldKind.PlayTime:
                    PlayTime = field.IntValue;
                    break;

                case LazerIpcFieldKind.Mods:
                    Mods = field.UIntValue;
                    break;

                case LazerIpcFieldKind.Combo:
                    Combo = field.IntValue;
                    break;

                case LazerIpcFieldKind.Score:
                    Score = field.IntValue;
                    break;

                case LazerIpcFieldKind.IsReplay:
                    IsReplay = field.BoolValue;
                    break;

                case LazerIpcFieldKind.Username:
                    Username = field.StringValue;
                    break;

                case LazerIpcFieldKind.BeatmapFolder:
                    BeatmapFolder = field.StringValue;
                    break;

                case LazerIpcFieldKind.BeatmapFilename:
                    BeatmapFilename = field.StringValue;
                    break;

                case LazerIpcFieldKind.BeatmapFiles:
                    BeatmapFiles = field.FilesValue ?? [];
                    break;

                case LazerIpcFieldKind.Statistics:
                    Statistics = field.StatisticsValue.ToSyncStatistics();
                    break;

                case LazerIpcFieldKind.HitErrors:
                    HitErrorIndex = field.IntValue;
                    HitErrors = field.IntArrayValue ?? [];
                    break;
            }
        }
    }
}

public enum LazerIpcFieldKind : byte
{
    ProcessId = 1,
    Status = 2,
    PlayTime = 3,
    Mods = 4,
    Combo = 5,
    Score = 6,
    IsReplay = 7,
    Username = 8,
    BeatmapFolder = 9,
    BeatmapFilename = 10,
    BeatmapFiles = 11,
    Statistics = 12,
    HitErrors = 13,
}

public sealed partial class LazerIpcDeltaFrame
{
    public int Version { get; set; }
    public LazerIpcDeltaField[] Fields { get; set; } = [];

    public static LazerIpcDeltaFrame Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new PayloadReader(payload);
        var version = reader.ReadInt32();
        var fieldCount = reader.ReadInt32();
        if (fieldCount is < 0 or > 64)
        {
            throw new InvalidDataException($"Invalid lazer IPC field count: {fieldCount}.");
        }

        var fields = new LazerIpcDeltaField[fieldCount];
        for (var i = 0; i < fields.Length; i++)
        {
            var field = new LazerIpcDeltaField { Kind = (LazerIpcFieldKind)reader.ReadByte() };
            switch (field.Kind)
            {
                case LazerIpcFieldKind.ProcessId:
                case LazerIpcFieldKind.Status:
                case LazerIpcFieldKind.PlayTime:
                case LazerIpcFieldKind.Combo:
                case LazerIpcFieldKind.Score:
                    field.IntValue = reader.ReadInt32();
                    break;

                case LazerIpcFieldKind.Mods:
                    field.UIntValue = reader.ReadUInt32();
                    break;

                case LazerIpcFieldKind.IsReplay:
                    field.BoolValue = reader.ReadByte() != 0;
                    break;

                case LazerIpcFieldKind.Username:
                case LazerIpcFieldKind.BeatmapFolder:
                case LazerIpcFieldKind.BeatmapFilename:
                    field.StringValue = reader.ReadString();
                    break;

                case LazerIpcFieldKind.BeatmapFiles:
                    field.FilesValue = reader.ReadFiles();
                    break;

                case LazerIpcFieldKind.Statistics:
                    field.StatisticsValue = reader.ReadStatistics();
                    break;

                case LazerIpcFieldKind.HitErrors:
                    field.IntValue = reader.ReadInt32();
                    field.IntArrayValue = reader.ReadInt32Array();
                    break;

                default:
                    throw new InvalidDataException($"Unsupported lazer IPC field kind: {field.Kind}.");
            }

            fields[i] = field;
        }

        reader.EnsureEnd();
        return new LazerIpcDeltaFrame
        {
            Version = version,
            Fields = fields,
        };
    }

    public bool HasField(LazerIpcFieldKind kind)
    {
        foreach (var field in Fields)
        {
            if (field.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }
}

public partial struct LazerIpcDeltaField
{
    public LazerIpcFieldKind Kind { get; set; }
    public int IntValue { get; set; }
    public uint UIntValue { get; set; }
    public bool BoolValue { get; set; }
    public string? StringValue { get; set; }
    public LazerIpcFile[]? FilesValue { get; set; }
    public LazerIpcStatistics StatisticsValue { get; set; }
    public int[]? IntArrayValue { get; set; }
}

public sealed partial class LazerIpcFile
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public partial struct LazerIpcStatistics
{
    public int Perfect { get; set; }
    public int Great { get; set; }
    public int Good { get; set; }
    public int Ok { get; set; }
    public int Meh { get; set; }
    public int Miss { get; set; }

    public SyncStatistics ToSyncStatistics()
        => new(Perfect, Great, Good, Ok, Meh, Miss);
}

file ref struct PayloadReader
{
    private readonly ReadOnlySpan<byte> payload;
    private int offset;

    public PayloadReader(ReadOnlySpan<byte> payload)
    {
        this.payload = payload;
    }

    public byte ReadByte()
    {
        EnsureAvailable(sizeof(byte));
        return payload[offset++];
    }

    public int ReadInt32()
    {
        EnsureAvailable(sizeof(int));
        var value = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
        offset += sizeof(int);
        return value;
    }

    public uint ReadUInt32()
    {
        EnsureAvailable(sizeof(uint));
        var value = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(offset, sizeof(uint)));
        offset += sizeof(uint);
        return value;
    }

    public string? ReadString()
    {
        var byteCount = ReadInt32();
        if (byteCount < 0)
        {
            if (byteCount == -1)
            {
                return null;
            }

            throw new InvalidDataException($"Invalid lazer IPC string length: {byteCount}.");
        }

        EnsureAvailable(byteCount);
        var value = Encoding.UTF8.GetString(payload.Slice(offset, byteCount));
        offset += byteCount;
        return value;
    }

    public LazerIpcFile[]? ReadFiles()
    {
        var count = ReadInt32();
        if (count < 0)
        {
            if (count == -1)
            {
                return null;
            }

            throw new InvalidDataException($"Invalid lazer IPC file count: {count}.");
        }

        if (count > 100_000)
        {
            throw new InvalidDataException($"Invalid lazer IPC file count: {count}.");
        }

        var files = new LazerIpcFile[count];
        for (var i = 0; i < files.Length; i++)
        {
            files[i] = new LazerIpcFile
            {
                Name = ReadString() ?? string.Empty,
                Path = ReadString() ?? string.Empty,
            };
        }

        return files;
    }

    public LazerIpcStatistics ReadStatistics()
        => new()
        {
            Perfect = ReadInt32(),
            Great = ReadInt32(),
            Good = ReadInt32(),
            Ok = ReadInt32(),
            Meh = ReadInt32(),
            Miss = ReadInt32(),
        };

    public int[]? ReadInt32Array()
    {
        var count = ReadInt32();
        if (count < 0)
        {
            if (count == -1)
            {
                return null;
            }

            throw new InvalidDataException($"Invalid lazer IPC int array length: {count}.");
        }

        if (count > 100_000)
        {
            throw new InvalidDataException($"Invalid lazer IPC int array length: {count}.");
        }

        var values = new int[count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadInt32();
        }

        return values;
    }

    public void EnsureEnd()
    {
        if (offset != payload.Length)
        {
            throw new InvalidDataException("Lazer IPC frame has trailing bytes.");
        }
    }

    private void EnsureAvailable(int count)
    {
        if (count < 0 || count > payload.Length - offset)
        {
            throw new InvalidDataException("Lazer IPC frame ended unexpectedly.");
        }
    }
}
