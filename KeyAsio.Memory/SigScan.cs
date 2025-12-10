using System.Diagnostics;
using System.Runtime.Versioning;
using Reloaded.Memory.Sigscan;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.System.SystemInformation;
using Windows.Win32.System.Threading;

namespace KeyAsio.Memory;

[SupportedOSPlatform("windows5.1.2600")]
public sealed class SigScan : IDisposable, ISigScan
{
    private readonly Process _process;
    private readonly List<CachedMemoryRegion> _memoryRegions = new();
    private bool _isDisposed;

    public SigScan(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        InitMemoryRegionInfo();
    }

    public unsafe nint FindPattern(string pattern, int offset = 0)
    {
        EnsureMemoryDumpedAndScannersReady();

        nint foundAddress = nint.Zero;

        foreach (var region in _memoryRegions)
        {
            if (foundAddress != nint.Zero || region.ReloadedScanner == null)
            {
                continue;
            }

            var result = region.ReloadedScanner.FindPattern(pattern);
            if (result.Found)
            {
                long finalAddress = (long)region.BaseAddress + result.Offset + offset;
                return new nint(finalAddress);
            }
        }

        //Parallel.ForEach(_memoryRegions, (region, state) =>
        //{
        //    if (foundAddress != nint.Zero || region.ReloadedScanner == null)
        //        return;
        //    var result = region.ReloadedScanner.FindPattern(pattern);

        //    long finalAddress = (long)region.BaseAddress + result.Offset + offset;

        //    if (Interlocked.CompareExchange(ref foundAddress, new nint(finalAddress), nint.Zero) == nint.Zero)
        //    {
        //        state.Stop();
        //    }
        //});

        return foundAddress;
    }

    public void Reload()
    {
        ResetRegion();
        InitMemoryRegionInfo();
    }

    public void ResetRegion()
    {
        foreach (var region in _memoryRegions)
        {
            region.Dispose();
        }

        _memoryRegions.Clear();
    }

    public bool ReadMemory(nint address, Span<byte> buffer, int size, out int bytesRead)
    {
        return ReadProcessMemory(_process.Handle, address, buffer, (uint)size, out bytesRead);
    }

    private unsafe void EnsureMemoryDumpedAndScannersReady()
    {
        if (_process.HasExited) return;
        HANDLE hProcess = (HANDLE)_process.Handle;

        foreach (var region in _memoryRegions)
        {
            if (region.ReloadedScanner != null) continue;

            region.DumpedRegion = new byte[region.RegionSize];
            fixed (byte* bufferPtr = region.DumpedRegion)
            {
                nuint bytesRead = 0;

                BOOL success = PInvoke.ReadProcessMemory(
                    hProcess,
                    region.BaseAddress,
                    bufferPtr,
                    (nuint)region.RegionSize,
                    &bytesRead
                );

                if (!success || bytesRead != (nuint)region.RegionSize)
                {
                    region.DumpedRegion = null;
                    continue;
                }
            }

            region.ReloadedScanner = new Scanner(region.DumpedRegion);
        }
    }

    private unsafe void InitMemoryRegionInfo()
    {
        if (_process.HasExited) return;

        PInvoke.GetSystemInfo(out SYSTEM_INFO sysInfo);

        void* minAddr = sysInfo.lpMinimumApplicationAddress;
        void* maxAddr = sysInfo.lpMaximumApplicationAddress;

        HANDLE hProcess = PInvoke.OpenProcess(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION | PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ,
            false,
            (uint)_process.Id
        );

        if (hProcess.IsNull) return;

        try
        {
            MEMORY_BASIC_INFORMATION memInfo = default;
            byte* currentPtr = (byte*)minAddr;

            while (currentPtr < (byte*)maxAddr)
            {
                nuint size = PInvoke.VirtualQueryEx(hProcess, currentPtr, &memInfo,
                    (nuint)sizeof(MEMORY_BASIC_INFORMATION));

                if (size == 0) break;

                bool isCommit = memInfo.State == VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT;
                bool isJitPage = (memInfo.Protect & PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE) != 0;

                if (isCommit && isJitPage)
                {
                    var region = new CachedMemoryRegion
                    {
                        BaseAddress = memInfo.BaseAddress,
                        RegionSize = memInfo.RegionSize
                    };
                    _memoryRegions.Add(region);
                }

                currentPtr += memInfo.RegionSize;
            }
        }
        finally
        {
            PInvoke.CloseHandle(hProcess);
        }
    }

    private static unsafe bool ReadProcessMemory(nint hProcess,
        nint lpBaseAddress,
        Span<byte> lpBuffer,
        uint dwSize,
        out int lpNumberOfBytesRead)
    {
        HANDLE handle = (HANDLE)hProcess;
        void* baseAddr = (void*)lpBaseAddress;

        nuint bytesReadNative = 0;

        fixed (byte* bufferPtr = lpBuffer)
        {
            BOOL success = PInvoke.ReadProcessMemory(
                handle,
                baseAddr,
                bufferPtr,
                dwSize,
                &bytesReadNative
            );

            lpNumberOfBytesRead = (int)bytesReadNative;

            return success;
        }
    }

    ~SigScan()
    {
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                ResetRegion();
            }

            _isDisposed = true;
        }
    }

    private class CachedMemoryRegion : IDisposable
    {
        public unsafe void* BaseAddress { get; set; }
        public ulong RegionSize { get; set; }
        public byte[]? DumpedRegion { get; set; }
        public Scanner? ReloadedScanner { get; set; }

        public void Dispose()
        {
            ReloadedScanner?.Dispose();
            ReloadedScanner = null;
            DumpedRegion = null;
        }
    }
}