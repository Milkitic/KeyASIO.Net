using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sigscan.Definitions.Structs;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.System.SystemInformation;
using Windows.Win32.System.Threading;

namespace KeyAsio.Core.Memory;

[SupportedOSPlatform("windows5.1.2600")]
public sealed class SigScan : IDisposable, ISigScan, IMemoryReader
{
    private readonly Process _process;
    private HANDLE _processHandle;
    private int _memoryRegionsMaxSize;
    private readonly List<MemoryRegionMetadata> _memoryRegions = new(256);
    private bool _isDisposed;

    public SigScan(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _processHandle = PInvoke.OpenProcess(
            PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ | PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION,
            false,
            (uint)process.Id);

        RefreshMemoryRegions();
    }

    public unsafe IntPtr FindPattern(string pattern, int offset = 0)
    {
        if (_process.HasExited || _memoryRegions.Count == 0) return IntPtr.Zero;

        IntPtr foundAddress = IntPtr.Zero;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(_memoryRegionsMaxSize);
        try
        {
            fixed (byte* bufPtr = buffer)
            {
                // Must be done sequentially
                foreach (ref MemoryRegionMetadata region in CollectionsMarshal.AsSpan(_memoryRegions))
                {
                    if (region.RegionSize > int.MaxValue) continue;

                    int size = (int)region.RegionSize;

                    BOOL success = PInvoke.ReadProcessMemory(
                        _processHandle,
                        region.BaseAddress,
                        bufPtr,
                        (nuint)size,
                        null);
                    if (!success)
                    {
                        continue;
                    }

                    using Scanner scanner = new Scanner(bufPtr, size);
                    PatternScanResult result = scanner.FindPattern(pattern);

                    if (!result.Found) continue;

                    long finalAddress = (long)region.BaseAddress + result.Offset + offset;
                    foundAddress = new IntPtr(finalAddress);
                    break;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return foundAddress;
    }

    public void Reload()
    {
        ResetRegion();
        RefreshMemoryRegions();
    }

    public void ResetRegion()
    {
        _memoryRegions.Clear();
        _memoryRegionsMaxSize = 0;
    }

    public bool ReadMemory(IntPtr address, Span<byte> buffer, int size, out int bytesRead)
    {
        return ReadProcessMemory(_processHandle, address, buffer, (uint)size, out bytesRead);
    }

    private static unsafe bool ReadProcessMemory(HANDLE hProcess,
        IntPtr lpBaseAddress,
        Span<byte> lpBuffer,
        uint dwSize,
        out int lpNumberOfBytesRead)
    {
        void* baseAddr = (void*)lpBaseAddress;

        nuint bytesReadNative = 0;

        fixed (byte* bufferPtr = lpBuffer)
        {
            BOOL success = PInvoke.ReadProcessMemory(
                hProcess,
                baseAddr,
                bufferPtr,
                dwSize,
                &bytesReadNative
            );

            lpNumberOfBytesRead = (int)bytesReadNative;

            return success;
        }
    }

    private unsafe void RefreshMemoryRegions()
    {
        _memoryRegions.Clear();
        if (_process.HasExited) return;

        PInvoke.GetSystemInfo(out SYSTEM_INFO sysInfo);
        byte* minAddr = (byte*)sysInfo.lpMinimumApplicationAddress;
        byte* maxAddr = (byte*)sysInfo.lpMaximumApplicationAddress;
        byte* currentPtr = minAddr;

        MEMORY_BASIC_INFORMATION memInfo = default;
        int maxSize = 0;

        while (currentPtr < maxAddr)
        {
            UIntPtr size = PInvoke.VirtualQueryEx(
                _processHandle,
                currentPtr,
                &memInfo,
                (nuint)sizeof(MEMORY_BASIC_INFORMATION));
            if (size == 0) break;

            bool isCommit = memInfo.State == VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT;
            bool isExecutable = (memInfo.Protect & (PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READ |
                                                    PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE |
                                                    PAGE_PROTECTION_FLAGS.PAGE_EXECUTE)) != 0;

            if (isCommit && isExecutable)
            {
                _memoryRegions.Add(new MemoryRegionMetadata(memInfo.BaseAddress, memInfo.RegionSize));
                maxSize = Math.Max(maxSize, (int)memInfo.RegionSize);
            }

            currentPtr += memInfo.RegionSize;
        }

        _memoryRegionsMaxSize = maxSize;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            ResetRegion();
        }

        if (_processHandle != HANDLE.Null)
        {
            PInvoke.CloseHandle(_processHandle);
            _processHandle = default;
        }

        _isDisposed = true;
    }

    private readonly unsafe struct MemoryRegionMetadata(void* baseAddress, UIntPtr regionSize)
    {
        public readonly void* BaseAddress = baseAddress;
        public readonly nuint RegionSize = regionSize;
    }
}