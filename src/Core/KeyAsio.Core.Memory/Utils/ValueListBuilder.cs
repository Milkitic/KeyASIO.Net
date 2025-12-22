using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace KeyAsio.Memory.Utils;

/// <summary>
/// Provides a lightweight, stack-friendly builder for a list of values
/// backed by an existing <see cref="Span{T}"/> or a pooled array.
/// </summary>
/// <remarks>
/// This type is a <c>ref struct</c> intended for short-lived, high-performance
/// scenarios. When constructed with a pooled array, call <see cref="Dispose"/>
/// to return the buffer to the shared <see cref="ArrayPool{T}"/>. This type is
/// not thread-safe and must not be used across async/await boundaries.
/// </remarks>
public ref struct ValueListBuilder<T> : IDisposable
{
    private Span<T> _span;
    private T[]? _arrayFromPool;
    private int _pos;

    /// <summary>
    /// Initializes the builder using the provided span and an optional starting position.
    /// </summary>
    /// <param name="initialSpan">The initial storage to use for the builder.</param>
    /// <param name="pos">The initial number of items considered written into the builder.</param>
    public ValueListBuilder(Span<T> initialSpan, int pos = 0)
    {
        _span = initialSpan;
        _arrayFromPool = null;
        _pos = pos;
    }

    /// <summary>
    /// Initializes the builder by renting a buffer of at least <paramref name="minimumLength"/> from the array pool.
    /// </summary>
    /// <param name="minimumLength">The minimum capacity to rent for the builder.</param>
    /// <remarks>Call <see cref="Dispose"/> to return the rented buffer to the pool.</remarks>
    public ValueListBuilder(int minimumLength = 256)
    {
        T[] array = ArrayPool<T>.Shared.Rent(minimumLength);
        _span = _arrayFromPool = array;
    }

    /// <summary>
    /// Gets or sets the number of items currently contained in the builder.
    /// </summary>
    /// <remarks>Setting the value trims or extends the logical length within the existing capacity.</remarks>
    public int Length
    {
        get => _pos;
        set
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= _span.Length);
            _pos = value;
        }
    }

    /// <summary>
    /// Gets a reference to the item at the specified index.
    /// </summary>
    /// <param name="index">A zero-based index less than <see cref="Length"/>.</param>
    /// <returns>A reference to the item at the given index.</returns>
    public ref T this[int index]
    {
        get
        {
            Debug.Assert(index < _pos);
            return ref _span[index];
        }
    }

    /// <summary>
    /// Appends a single item to the end of the builder, growing the internal buffer if required.
    /// </summary>
    /// <param name="item">The item to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(T item)
    {
        int pos = _pos;
        if (pos >= _span.Length)
            Grow();

        _span[pos] = item;
        _pos = pos + 1;
    }

    /// <summary>
    /// Appends a sequence of items to the builder, growing the internal buffer as needed.
    /// </summary>
    /// <param name="items">The items to append to the builder.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendSpan(scoped ReadOnlySpan<T> items)
    {
        int currentPos = _pos;
        int itemsLength = items.Length;

        // Check if we need to grow the buffer
        if (currentPos + itemsLength > _span.Length)
        {
            // Calculate the new size ensuring it can fit all the new items
            int newSize = Math.Max(_span.Length * 2, currentPos + itemsLength);
            T[] array = ArrayPool<T>.Shared.Rent(newSize);

            bool success = _span.TryCopyTo(array);
            Debug.Assert(success);

            T[]? toReturn = _arrayFromPool;
            _span = _arrayFromPool = array;

            if (toReturn != null)
            {
                ArrayPool<T>.Shared.Return(toReturn);
            }
        }

        // Copy the items to the span
        items.CopyTo(_span.Slice(currentPos));
        _pos = currentPos + itemsLength;
    }

    /// <summary>
    /// Returns a read-only span over the items written so far.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{T}"/> of length <see cref="Length"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> AsSpan()
    {
        return _span.Slice(0, _pos);
    }

    /// <summary>
    /// Returns a writable span over the items written so far.
    /// </summary>
    /// <remarks>
    /// Mutating the returned span directly modifies the builder's contents. Do not use this after
    /// <see cref="Dispose"/>.
    /// </remarks>
    /// <returns>A <see cref="Span{T}"/> of length <see cref="Length"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpanUnsafe()
    {
        return _span.Slice(0, _pos);
    }

    /// <summary>
    /// Clears the contents of the builder and resets <see cref="Length"/> to zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _span.Clear();
        _pos = 0;
    }

    /// <summary>
    /// Returns any rented buffer to the array pool and invalidates the instance for further use.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        T[]? toReturn = _arrayFromPool;
        if (toReturn != null)
        {
            _arrayFromPool = null;
            ArrayPool<T>.Shared.Return(toReturn);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Grow()
    {
        T[] array = ArrayPool<T>.Shared.Rent(_span.Length * 2);

        bool success = _span.TryCopyTo(array);
        Debug.Assert(success);

        T[]? toReturn = _arrayFromPool;
        _span = _arrayFromPool = array;
        if (toReturn != null)
        {
            ArrayPool<T>.Shared.Return(toReturn);
        }
    }
}