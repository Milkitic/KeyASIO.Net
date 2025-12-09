namespace KeyAsio.Shared.Utils;

public class EphemeralLineReader : IDisposable
{
    private readonly TextReader _reader;
    private char[] _lineReturnBuffer; // Buffer for the line to be returned
    private readonly int _initialLineReturnBufferSize; // Store the initial config for _lineReturnBuffer

    private char[] _internalCharBuffer; // Buffer for reading blocks from _reader
    private int _internalCharPos; // Current position in _internalCharBuffer
    private int _internalCharLen; // Number of valid chars in _internalCharBuffer

    private const int DefaultInitialLineReturnBufferSize = 256;
    private const int DefaultInternalReaderBufferSize = 1024; // Similar to StreamReader's internal buffer
    private const int ArrayMaxLength = 0X7FFFFFC7;

    public EphemeralLineReader(TextReader reader,
        int initialLineCapacity = DefaultInitialLineReturnBufferSize,
        int internalReaderBufferSize = DefaultInternalReaderBufferSize)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        if (initialLineCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialLineCapacity),
                "Initial line capacity must be positive.");
        if (internalReaderBufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(internalReaderBufferSize),
                "Internal reader buffer size must be positive.");

        _initialLineReturnBufferSize = initialLineCapacity;
        _lineReturnBuffer = new char[initialLineCapacity];

        _internalCharBuffer = new char[internalReaderBufferSize];
        _internalCharPos = 0;
        _internalCharLen = 0;
    }

    /// <summary>
    /// 从文本读取器中读取一行字符，并将数据作为 ReadOnlyMemory&lt;char&gt; 返回。
    /// 返回的内存仅在下一次调用 ReadLine 之前有效。
    /// </summary>
    /// <returns>一个包含来自文本读取器的下一行的 ReadOnlyMemory&lt;char&gt;；如果已到达读取器的末尾，则为 null。</returns>
    public ReadOnlyMemory<char>? ReadLine()
    {
        int lineReturnBufferPos = 0; // Current position in _lineReturnBuffer

        while (true)
        {
            // If internal buffer is exhausted, fill it
            if (_internalCharPos >= _internalCharLen)
            {
                FillInternalBuffer();
                // If FillInternalBuffer brings no new data (EOF)
                if (_internalCharLen == 0)
                {
                    // If there's pending data in _lineReturnBuffer, return it as the last line
                    if (lineReturnBufferPos > 0)
                        return new ReadOnlyMemory<char>(_lineReturnBuffer, 0, lineReturnBufferPos);
                    // else, return null (EOF)
                    return null;
                }
            }

            // Scan for EOL in the current _internalCharBuffer chunk
            for (int i = _internalCharPos; i < _internalCharLen; i++)
            {
                char ch = _internalCharBuffer[i];
                if (ch is '\n' or '\r')
                {
                    // EOL found. Copy data from _internalCharBuffer to _lineReturnBuffer.
                    int countToCopy = i - _internalCharPos;
                    EnsureLineReturnBufferCapacity(lineReturnBufferPos + countToCopy);
                    Array.Copy(_internalCharBuffer, _internalCharPos, _lineReturnBuffer, lineReturnBufferPos,
                        countToCopy);
                    lineReturnBufferPos += countToCopy;
                    _internalCharPos = i + 1; // Consume EOL char(s)

                    if (ch == '\r')
                    {
                        // Check for \n following \r
                        if (_internalCharPos < _internalCharLen) // If \n is in current _internalCharBuffer
                        {
                            if (_internalCharBuffer[_internalCharPos] == '\n')
                            {
                                _internalCharPos++; // Consume \n
                            }
                        }
                        else // \r was at end of _internalCharBuffer, need to peek next buffer
                        {
                            FillInternalBuffer(); // Read more data
                            if (_internalCharLen > 0 &&
                                _internalCharBuffer[0] == '\n') // Check first char of new buffer
                            {
                                _internalCharPos =
                                    1; // Consumed \n from new buffer (_internalCharPos was reset to 0 by FillInternalBuffer)
                            }
                        }
                    }

                    return new ReadOnlyMemory<char>(_lineReturnBuffer, 0, lineReturnBufferPos);
                }
            }

            // No EOL in current _internalCharBuffer chunk. Copy whole chunk to _lineReturnBuffer.
            int remainingInInternalBuffer = _internalCharLen - _internalCharPos;
            EnsureLineReturnBufferCapacity(lineReturnBufferPos + remainingInInternalBuffer);
            Array.Copy(_internalCharBuffer, _internalCharPos, _lineReturnBuffer, lineReturnBufferPos,
                remainingInInternalBuffer);
            lineReturnBufferPos += remainingInInternalBuffer;
            _internalCharPos = _internalCharLen; // Mark internal buffer as fully consumed
            // Loop will continue, and FillInternalBuffer will be called if needed.
        }
    }

    public void Dispose()
    {
        _reader.Dispose();
        // If _lineReturnBuffer or _internalCharBuffer were from ArrayPool, return them here.
        // For now, they are managed by GC as normal arrays.
    }

    private void FillInternalBuffer()
    {
        _internalCharPos = 0;
        _internalCharLen = _reader.Read(_internalCharBuffer, 0, _internalCharBuffer.Length);
    }

    private void EnsureLineReturnBufferCapacity(int requiredCapacity)
    {
        if (requiredCapacity > _lineReturnBuffer.Length)
        {
            int newSize;
            // Determine base for new size: either double current, or use initial configured size if current is somehow 0
            if (_lineReturnBuffer.Length == 0)
            {
                newSize = Math.Max(requiredCapacity, _initialLineReturnBufferSize);
            }
            else
            {
                newSize = _lineReturnBuffer.Length * 2;
                // Check for overflow if _lineReturnBuffer.Length is very large
                if (newSize < _lineReturnBuffer.Length || newSize <= 0) newSize = ArrayMaxLength;
            }

            // Ensure newSize is at least the required capacity
            if (newSize < requiredCapacity)
            {
                newSize = requiredCapacity;
            }

            // Apply a growth cap similar to the original logic (e.g., 1MB chunks if already large)
            if (_lineReturnBuffer.Length > 1_048_576 && newSize > _lineReturnBuffer.Length + 1_048_576)
            {
                newSize = _lineReturnBuffer.Length + 1_048_576;
                // Still ensure it's large enough for the current requirement after capping
                if (newSize < requiredCapacity) newSize = requiredCapacity;
            }

            // Final check against max array size and ensure it's still >= requiredCapacity
            if (newSize > ArrayMaxLength) newSize = ArrayMaxLength;
            if (newSize < requiredCapacity)
            {
                // This implies requiredCapacity > Array.MaxLength, which is an unrecoverable situation for Array.Resize
                throw new OutOfMemoryException("Line is too long to fit in memory buffer.");
            }

            Array.Resize(ref _lineReturnBuffer, newSize);
        }
    }
}