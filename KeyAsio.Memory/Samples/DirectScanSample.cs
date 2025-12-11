using System.Diagnostics;
using static KeyAsio.Memory.MemoryReadHelper;

namespace KeyAsio.Memory.Samples;

internal static class DirectScanSample
{
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

    public static async ValueTask Perform()
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

        // Ints
        var id = GetValue<int>(sigScan, beatmapClassAddress + 0xC8);
        var setId = GetValue<int>(sigScan, beatmapClassAddress + 0xCC);

        // Strings
        var mapString = GetManagedString(sigScan, beatmapClassAddress + 0x80);
        var folderName = GetManagedString(sigScan, beatmapClassAddress + 0x78);
        var osuFileName = GetManagedString(sigScan, beatmapClassAddress + 0x90);
        var md5 = GetManagedString(sigScan, beatmapClassAddress + 0x6C);

        // Floats
        var ar = GetValue<float>(sigScan, beatmapClassAddress + 0x2C);
        var cs = GetValue<float>(sigScan, beatmapClassAddress + 0x30);
        var hp = GetValue<float>(sigScan, beatmapClassAddress + 0x34);
        var od = GetValue<float>(sigScan, beatmapClassAddress + 0x38);

        // Short
        var status = GetValue<short>(sigScan, beatmapClassAddress + 0x12C);

        Console.WriteLine($"ID: {id}");
        Console.WriteLine($"SetID: {setId}");
        Console.WriteLine($"MapString: {mapString}");
        Console.WriteLine($"FolderName: {folderName}");
        Console.WriteLine($"OsuFileName: {osuFileName}");
        Console.WriteLine($"MD5: {md5}");
        Console.WriteLine($"AR: {ar}");
        Console.WriteLine($"CS: {cs}");
        Console.WriteLine($"HP: {hp}");
        Console.WriteLine($"OD: {od}");
        Console.WriteLine($"Status: {status}");

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine("General Data:");

        // GeneralData Patterns
        var statusPattern = sigScan.FindPattern("48 83 F8 04 73 1E");
        var settingsPattern = sigScan.FindPattern("83 E0 20 85 C0 7E 2F");
        var audioTimeBasePattern = sigScan.FindPattern("83 E4 F8 57 56 83 EC 38");
        var modsPattern = sigScan.FindPattern("C8 FF ?? ?? ?? ?? ?? 81 0D ?? ?? ?? ?? 00 08 00 00");
        var chatExpandedPattern = sigScan.FindPattern("0A D7 23 3C 00 00 ?? 01");

        // GeneralData Values
        if (statusPattern != IntPtr.Zero)
        {
            var statusAddr = GetPointer(sigScan, statusPattern - 0x4);
            var rawStatus = GetValue<int>(sigScan, statusAddr);
            Console.WriteLine($"RawStatus: {rawStatus}");
        }

        if (baseMemPos != IntPtr.Zero)
        {
            var gameModePtr = GetPointer(sigScan, baseMemPos - 0x33);
            var gameMode = GetValue<int>(sigScan, gameModePtr);
            var retries = GetValue<int>(sigScan, gameModePtr + 0x8);

            var audioTimePtr = GetPointer(sigScan, baseMemPos + 0x64);
            var audioTime = GetValue<int>(sigScan, audioTimePtr - 0x10);

            Console.WriteLine($"GameMode: {gameMode}");
            Console.WriteLine($"Retries: {retries}");
            Console.WriteLine($"AudioTime: {audioTime}");
        }

        if (audioTimeBasePattern != IntPtr.Zero)
        {
            var totalAudioTimeBasePtr = GetPointer(sigScan, audioTimeBasePattern + 0xA);
            var totalAudioTime = GetValue<double>(sigScan, totalAudioTimeBasePtr + 0x4);
            Console.WriteLine($"TotalAudioTime: {totalAudioTime}");
        }

        if (chatExpandedPattern != IntPtr.Zero)
        {
            var chatExpandedAddr = GetPointer(sigScan, chatExpandedPattern - 0x20);
            var chatIsExpanded = GetValue<bool>(sigScan, chatExpandedAddr);
            Console.WriteLine($"ChatIsExpanded: {chatIsExpanded}");
        }

        if (modsPattern != IntPtr.Zero)
        {
            var modsPtr = GetPointer(sigScan, modsPattern + 0x9);
            var mods = GetValue<int>(sigScan, modsPtr);
            Console.WriteLine($"Mods: {mods}");
        }

        if (settingsPattern != IntPtr.Zero)
        {
            var settingsPtr = GetPointer(sigScan, settingsPattern + 0x8);

            var showInterfaceAddr = GetPointer(sigScan, settingsPtr + 0x4);
            var showInterface = GetValue<bool>(sigScan, showInterfaceAddr + 0xC);
            Console.WriteLine($"ShowPlayingInterface: {showInterface}");

            var osuVersionAddr = GetPointer(sigScan, settingsPtr + 0x44);
            var osuVersion = GetManagedString(sigScan, osuVersionAddr + 0x4);
            Console.WriteLine($"OsuVersion: {osuVersion}");
        }

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine("Additional Data:");

        // Additional Patterns
        var isLoggedInPattern = sigScan.FindPattern("B8 0B 00 00 8B 35");
        var userPanelPattern = sigScan.FindPattern("FF FF 89 50 70");
        var currentSkinDataPattern = sigScan.FindPattern("85 C0 74 11 8B 1D");
        var currentRulesetPattern = sigScan.FindPattern("C7 86 48 01 00 00 01 00 00 00 A1");
        var isReplayPattern = sigScan.FindPattern("8B FA B8 01 00 00 00");

        // BanchoUser
        if (isLoggedInPattern != IntPtr.Zero)
        {
            var isLoggedInPtr = GetPointer(sigScan, isLoggedInPattern - 0xB);
            var isLoggedIn = GetValue<bool>(sigScan, isLoggedInPtr);
            Console.WriteLine($"IsLoggedIn: {isLoggedIn}");
        }

        if (userPanelPattern != IntPtr.Zero)
        {
            var userPanelPtr = GetPointer(sigScan, userPanelPattern + 0x6);
            var userPanelAddress = GetPointer(sigScan, userPanelPtr);

            if (userPanelAddress != IntPtr.Zero)
            {
                var username = GetManagedString(sigScan, userPanelAddress + 0x30);
                Console.WriteLine($"Username: {username}");
            }
        }

        // Skin
        if (currentSkinDataPattern != IntPtr.Zero)
        {
            var skinDataPtr = GetPointer(sigScan, currentSkinDataPattern + 0x6);
            var skinDataAddress = GetPointer(sigScan, skinDataPtr);
            if (skinDataAddress != IntPtr.Zero)
            {
                var folder = GetManagedString(sigScan, skinDataAddress + 0x44);
                Console.WriteLine($"SkinFolder: {folder}");
            }
        }

        // Player / RulesetPlayData
        if (isReplayPattern != IntPtr.Zero)
        {
            var isReplayPtr = GetPointer(sigScan, isReplayPattern + 0x2A);
            var isReplay = GetValue<bool>(sigScan, isReplayPtr);
            Console.WriteLine($"IsReplay: {isReplay}");
        }

        if (currentRulesetPattern != IntPtr.Zero)
        {
            var tempPtr = GetPointer(sigScan, currentRulesetPattern + 0xB);
            var currentRulesetPtr = tempPtr + 0x4;
            var currentRulesetAddress = GetPointer(sigScan, currentRulesetPtr);

            if (currentRulesetAddress != IntPtr.Zero)
            {
                var score = GetValue<int>(sigScan, currentRulesetAddress + 0x100);
                Console.WriteLine($"Score: {score}");

                var playerPtr = GetPointer(sigScan, currentRulesetAddress + 0x68);
                if (playerPtr != IntPtr.Zero)
                {
                    var comboBase = GetPointer(sigScan, playerPtr + 0x38);
                    if (comboBase != IntPtr.Zero)
                    {
                        var combo = GetValue<ushort>(sigScan, comboBase + 0x94);
                        Console.WriteLine($"Combo: {combo}");
                    }
                }
            }
        }

        //var cts = new CancellationTokenSource();
        //var task = Task.Factory.StartNew(async () =>
        //{
        //    var t = new PeriodicTimer(TimeSpan.FromMilliseconds(3));
        //    int oldAudioTime = int.MinValue;
        //    int i = 0;
        //    while (await t.WaitForNextTickAsync())
        //    {
        //        if (cts.IsCancellationRequested) break;
        //        if (baseMemPos != IntPtr.Zero)
        //        {
        //            var audioTimePtr = GetPointer(sigScan, baseMemPos + 0x64);
        //            var audioTime = GetValue<int>(sigScan, audioTimePtr - 0x10);
        //            if (oldAudioTime != audioTime || (oldAudioTime == audioTime && i < 10))
        //                Console.WriteLine($"AudioTime: {audioTime}");

        //            if (oldAudioTime == audioTime)
        //                i++;
        //            else
        //                i = 0;
        //            oldAudioTime = audioTime;
        //        }
        //    }
        //}, TaskCreationOptions.LongRunning);

        //Console.ReadKey();
        //cts.Cancel();
        //await task;
    }
}