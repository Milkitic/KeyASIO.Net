using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyAsio.Shared.Services;

public sealed class RtssOsdWriter : IDisposable
{
    private readonly string _entryName;
    private uint _osdSlot;
    private bool _disposed;

    public RtssOsdWriter(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            throw new ArgumentException("Entry name cannot be null, empty, or whitespace.", nameof(entryName));

        var bytes = Encoding.ASCII.GetBytes(entryName);
        if (bytes.Length > 255)
            throw new ArgumentException("Entry name exceeds max length of 255 when converted to ANSI.", nameof(entryName));

        _entryName = entryName;
        _osdSlot = 0;

        // Verify RTSS is accessible
        using var mmf = OpenSharedMemory();
    }

    public void Update(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(text);

        var textBytes = Encoding.ASCII.GetBytes(text);
        if (textBytes.Length > 4095)
            throw new ArgumentException("Text exceeds max length of 4095 when converted to ANSI.", nameof(text));

        using var mmf = OpenSharedMemory();
        using var accessor = mmf.CreateViewAccessor();

        var signature = accessor.ReadUInt32(0);
        if (signature != 0x52545353) // 'RTSS' in little-endian: 'R'(0x52) 'T'(0x54) 'S'(0x53) 'S'(0x53)
            throw new InvalidOperationException("Invalid RTSS shared memory signature.");

        var version = accessor.ReadUInt32(4);
        if (version < 0x00020000)
            throw new InvalidOperationException("Unsupported RTSS shared memory version.");

        var osdEntrySize = accessor.ReadUInt32(20);
        var osdArrOffset = accessor.ReadUInt32(24);
        var osdArrSize = accessor.ReadUInt32(28);

        // Start at either our previously used slot, or the top (skip slot 0 which is primary/global)
        for (uint i = (_osdSlot == 0 ? 1 : _osdSlot); i < osdArrSize; i++)
        {
            var entryOffset = osdArrOffset + (i * osdEntrySize);

            // Read current owner
            var ownerBytes = new byte[256];
            accessor.ReadArray(entryOffset + 256, ownerBytes, 0, 256);
            var owner = Encoding.ASCII.GetString(ownerBytes).TrimEnd('\0');

            // If we need a new slot and this one is unused, claim it
            if (_osdSlot == 0 && string.IsNullOrEmpty(owner))
            {
                _osdSlot = i;
                var ownerNameBytes = new byte[256];
                Encoding.ASCII.GetBytes(_entryName).CopyTo(ownerNameBytes, 0);
                accessor.WriteArray(entryOffset + 256, ownerNameBytes, 0, 256);
                owner = _entryName;
            }

            // If this is our slot
            if (owner == _entryName)
            {
                // Use extended text slot for v2.7 and higher shared memory (4096 symbols)
                if (version >= 0x00020007)
                {
                    var osdExBytes = new byte[4096];
                    textBytes.CopyTo(osdExBytes, 0);
                    accessor.WriteArray(entryOffset + 512, osdExBytes, 0, 4096);
                }
                else
                {
                    var osdBytes = new byte[256];
                    textBytes.CopyTo(osdBytes, 0);
                    accessor.WriteArray(entryOffset, osdBytes, 0, 256);
                }

                // Force OSD update
                var currentFrame = accessor.ReadUInt32(32);
                accessor.Write(32, currentFrame + 1);
                break;
            }

            // In case we lost our previously used slot, start over
            if (_osdSlot != 0)
            {
                _osdSlot = 0;
                i = 0; // will be incremented to 1 by the for loop
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            using var mmf = OpenSharedMemory();
            using var accessor = mmf.CreateViewAccessor();

            var osdEntrySize = accessor.ReadUInt32(20);
            var osdArrOffset = accessor.ReadUInt32(24);
            var osdArrSize = accessor.ReadUInt32(28);

            for (uint i = 1; i < osdArrSize; i++)
            {
                var entryOffset = osdArrOffset + (i * osdEntrySize);

                var ownerBytes = new byte[256];
                accessor.ReadArray(entryOffset + 256, ownerBytes, 0, 256);
                var owner = Encoding.ASCII.GetString(ownerBytes).TrimEnd('\0');

                if (owner == _entryName)
                {
                    // Zero out the entire entry
                    var zeroBytes = new byte[osdEntrySize];
                    accessor.WriteArray(entryOffset, zeroBytes, 0, (int)osdEntrySize);

                    // Force OSD update
                    var currentFrame = accessor.ReadUInt32(32);
                    accessor.Write(32, currentFrame + 1);
                }
            }
        }
        catch
        {
            // Ignored during disposal
        }
    }

    private static MemoryMappedFile OpenSharedMemory()
    {
        try
        {
            return MemoryMappedFile.OpenExisting("RTSSSharedMemoryV2", MemoryMappedFileRights.ReadWrite);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to open RTSS shared memory. Is RTSS running?", ex);
        }
    }
}
