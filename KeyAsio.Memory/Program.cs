using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyAsio.Memory;

// osu! 进程内存空间 (32-bit)
// │
// ├─ [内存区域: JIT 代码段 - PAGE_EXECUTE_READWRITE]
// │   │
// │   └─► [特征码扫描] "F8 01 74 04 83 65"
// │       └─► baseMemPos (锚点地址 - JIT编译的代码位置)
// │           │
// │           └─ [偏移 -0xC]
// │               └─► beatmapMemPos (静态/全局引用区)
// │
// ├─ [内存区域: 托管堆 Heap - CLR Managed Memory]
// │   │
// │   ├─► beatmapClassPointerAddress 
// │   │   └─ [Beatmap Manager/Holder 单例]
// │   │       └─► beatmapClassAddress 
// │   │           └─ [Beatmap Instance - 当前选中的谱面对象]
// │   │               │
// │   │               ├─ +0x00: beatmapClassHeader (vtable/类型标识)
// │   │               ├─ +0x04~0x8F: [其他属性字段]
// │   │               └─ +0x90: BeatmapFileName (string 引用)
// │   │
// │   └─► beatmapClassBeatmapFileNamePropStringPointer
// │       └─ [.NET String Object - Unicode]
// │           ├─ -0x04: [SyncBlock Index]
// │           ├─ +0x00: [vtable/类型标识]
// │           ├─ +0x04: Length = 字符数量
// │           └─ +0x08: Unicode字符数据 (UTF-16LE)
// │                   └─► beatmapFileNamePropValue (最终结果)


// Beatmap Manager (单例)
// └─ CurrentBeatmap → Beatmap Instance
//                      ├─ Header (类型信息)
//                      ├─ Metadata (艺术家、标题等)
//                      ├─ TimingPoints
//                      ├─ HitObjects
//                      └─ FileName (string, offset +0x90)
//                          └─ "example.osu"

public class Program
{
    private const int PtrSize = 4; // osu! is a 32-bit process
    private static readonly byte[] Buffer = new byte[1024];

    public static void Main(string[] args)
    {
        var process = Process.GetProcessesByName("osu!").FirstOrDefault();
        if (process == null)
        {
            throw new Exception("Process not found.");
        }

        using var sigScan = new SigScan(process);
        var baseMemPos = sigScan.FindPattern("F8 01 74 04 83 65");
        Debug.Assert(baseMemPos != nint.Zero);

        var beatmapMemPos = baseMemPos - 0xC;

        var beatmapClassPointerAddress = GetPointer(sigScan, beatmapMemPos);
        var beatmapClassAddress = GetPointer(sigScan, beatmapClassPointerAddress);
        var beatmapClassHeader = GetValue<int>(sigScan, beatmapClassAddress);
        var beatmapClassBeatmapFileNamePropAddress = beatmapClassAddress + 0x90;
        var beatmapClassBeatmapFileNamePropStringPointer = GetPointer(sigScan, beatmapClassBeatmapFileNamePropAddress);
        var beatmapClassBeatmapFileNamePropStringElementsCount =
            GetValue<int>(sigScan, beatmapClassBeatmapFileNamePropStringPointer + 4);
        var beatmapFileNamePropValue = GetString(sigScan, beatmapClassBeatmapFileNamePropStringPointer + 8,
            beatmapClassBeatmapFileNamePropStringElementsCount * 2);
    }

    private static string GetString(SigScan sigScan, IntPtr elementStartPointer, int bytesCount)
    {
        byte[]? buffer = null;
        var span = bytesCount < 256
            ? stackalloc byte[bytesCount]
            : buffer = ArrayPool<byte>.Shared.Rent(bytesCount);
        try
        {
            if (!sigScan.ReadMemory(elementStartPointer, span, bytesCount, out var bytesRead))
                throw new Exception("Failed to read memory.");
            if (bytesRead != bytesCount)
                throw new Exception("Failed to read complete string data.");
            return Encoding.Unicode.GetString(span.Slice(0, bytesCount));
        }
        finally
        {
            if (buffer != null) ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static T GetValue<T>(SigScan sigScan, IntPtr pointer) where T : struct
    {
        if (!sigScan.ReadMemory(pointer, Buffer, PtrSize, out _))
            throw new Exception("Failed to read memory.");

        var value = MemoryMarshal.Read<T>(Buffer);
        return value;
    }

    private static IntPtr GetPointer(SigScan sigScan, IntPtr parentPointer)
    {
        if (!sigScan.ReadMemory(parentPointer, Buffer, PtrSize, out _))
            throw new Exception("Failed to read memory.");

        var pointer = (nint)MemoryMarshal.Read<uint>(Buffer);
        return pointer;
    }
}