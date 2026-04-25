using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyAsio.Shared.Services;

public sealed class RtssOsdWriter : IDisposable
{
    private const string SharedMemoryName = "RTSSSharedMemoryV2";
    private const int OwnerFieldLength = 256;
    private const int LegacyTextLength = 256;
    private const int ExtendedTextLength = 4096;
    private const uint SupportedVersionMin = 0x00020000;
    private const uint ExtendedTextVersionMin = 0x00020007;
    private const uint RtssSignature = 0x52545353; // 'RTSS'

    private readonly string _entryName;
    private readonly byte[] _entryNameBytes = new byte[OwnerFieldLength];
    private readonly byte[] _ownerBuffer = new byte[OwnerFieldLength];
    private readonly byte[] _legacyTextBuffer = new byte[LegacyTextLength];
    private readonly byte[] _extendedTextBuffer = new byte[ExtendedTextLength];
    private readonly object _syncRoot = new();

    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private uint _osdEntrySize;
    private uint _osdArrOffset;
    private uint _osdArrSize;
    private bool _useExtendedText;

    private uint _osdSlot;
    private long _entryOffset;
    private bool _disposed;

    public RtssOsdWriter(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            throw new ArgumentException("Entry name cannot be null, empty, or whitespace.", nameof(entryName));

        var bytes = Encoding.ASCII.GetByteCount(entryName);
        if (bytes > OwnerFieldLength - 1)
            throw new ArgumentException("Entry name exceeds max length of 255 when converted to ANSI.", nameof(entryName));

        _entryName = entryName;
        _osdSlot = 0;
        _entryOffset = 0;

        Encoding.ASCII.GetBytes(entryName.AsSpan(), _entryNameBytes);

        lock (_syncRoot)
        {
            EnsureConnected();
        }
    }

    public void Update(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(text);

        lock (_syncRoot)
        {
            EnsureConnected();
            if (!TryEnsureSlot())
            {
                return;
            }

            var maxTextLength = _useExtendedText ? ExtendedTextLength - 1 : LegacyTextLength - 1;
            var textBytes = Encoding.ASCII.GetByteCount(text);
            if (textBytes > maxTextLength)
                throw new ArgumentException($"Text exceeds max length of {maxTextLength} when converted to ANSI.", nameof(text));

            var targetBuffer = _useExtendedText ? _extendedTextBuffer : _legacyTextBuffer;
            var written = Encoding.ASCII.GetBytes(text.AsSpan(), targetBuffer);

            var textOffset = _entryOffset + (_useExtendedText ? 512 : 0);
            _accessor!.WriteArray(textOffset, targetBuffer, 0, written);
            _accessor.Write(textOffset + written, (byte)0);

            IncrementFrameCounter();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            lock (_syncRoot)
            {
                if (_accessor != null && _osdSlot != 0 && IsSlotOwnedByEntry(_osdSlot))
                {
                    var zeroBytes = new byte[_osdEntrySize];
                    _accessor.WriteArray(_entryOffset, zeroBytes, 0, (int)_osdEntrySize);
                    IncrementFrameCounter();
                }
            }
        }
        catch
        {
            // Ignored during disposal
        }
        finally
        {
            _accessor?.Dispose();
            _accessor = null;
            _mmf?.Dispose();
            _mmf = null;
        }
    }

    private void EnsureConnected()
    {
        if (_accessor != null)
        {
            return;
        }

        _mmf = OpenSharedMemory();
        _accessor = _mmf.CreateViewAccessor();

        var signature = _accessor.ReadUInt32(0);
        if (signature != RtssSignature)
            throw new InvalidOperationException("Invalid RTSS shared memory signature.");

        var version = _accessor.ReadUInt32(4);
        if (version < SupportedVersionMin)
            throw new InvalidOperationException("Unsupported RTSS shared memory version.");

        _osdEntrySize = _accessor.ReadUInt32(20);
        _osdArrOffset = _accessor.ReadUInt32(24);
        _osdArrSize = _accessor.ReadUInt32(28);
        _useExtendedText = version >= ExtendedTextVersionMin;

        _osdSlot = 0;
        _entryOffset = 0;
    }

    private bool TryEnsureSlot()
    {
        if (_osdSlot != 0 && IsSlotOwnedByEntry(_osdSlot))
        {
            return true;
        }

        _osdSlot = 0;
        _entryOffset = 0;

        for (uint i = 1; i < _osdArrSize; i++)
        {
            var entryOffset = GetEntryOffset(i);
            var ownerOffset = entryOffset + OwnerFieldLength;

            _accessor!.ReadArray(ownerOffset, _ownerBuffer, 0, OwnerFieldLength);

            if (IsOwnerBufferEmpty())
            {
                _accessor.WriteArray(ownerOffset, _entryNameBytes, 0, OwnerFieldLength);
                _osdSlot = i;
                _entryOffset = entryOffset;
                return true;
            }

            if (IsOwnerBufferEntryName())
            {
                _osdSlot = i;
                _entryOffset = entryOffset;
                return true;
            }
        }

        return false;
    }

    private bool IsSlotOwnedByEntry(uint slot)
    {
        var entryOffset = GetEntryOffset(slot);
        _accessor!.ReadArray(entryOffset + OwnerFieldLength, _ownerBuffer, 0, OwnerFieldLength);
        if (!IsOwnerBufferEntryName())
        {
            return false;
        }

        _entryOffset = entryOffset;
        return true;
    }

    private static bool IsOwnerByteTerminator(byte value)
    {
        return value == 0;
    }

    private bool IsOwnerBufferEmpty()
    {
        return IsOwnerByteTerminator(_ownerBuffer[0]);
    }

    private bool IsOwnerBufferEntryName()
    {
        for (var i = 0; i < OwnerFieldLength; i++)
        {
            var actual = _ownerBuffer[i];
            var expected = _entryNameBytes[i];

            if (actual != expected)
            {
                return false;
            }

            if (IsOwnerByteTerminator(expected))
            {
                return true;
            }
        }

        return true;
    }

    private long GetEntryOffset(uint slot)
    {
        return _osdArrOffset + (long)slot * _osdEntrySize;
    }

    private void IncrementFrameCounter()
    {
        var currentFrame = _accessor!.ReadUInt32(32);
        _accessor.Write(32, currentFrame + 1);
    }

    private static MemoryMappedFile OpenSharedMemory()
    {
        try
        {
            return MemoryMappedFile.OpenExisting(SharedMemoryName, MemoryMappedFileRights.ReadWrite);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to open RTSS shared memory. Is RTSS running?", ex);
        }
    }
}
