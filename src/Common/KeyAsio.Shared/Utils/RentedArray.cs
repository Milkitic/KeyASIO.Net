using System.Buffers;

namespace KeyAsio.Shared.Utils;

/// <summary>
/// Represents a rented array from an <see cref="ArrayPool{T}"/> that returns itself to the pool when disposed.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
public readonly struct RentedArray<T> : IDisposable
{
    /// <summary>
    /// Gets the rented array instance.
    /// </summary>
    /// <value>The rented array containing elements of type <typeparamref name="T"/>.</value>
    public T[] Array { get; }

    private readonly ArrayPool<T> _pool;

    /// <summary>
    /// Initializes a new instance of the <see cref="RentedArray{T}"/> struct.
    /// </summary>
    /// <param name="pool">The array pool to rent from.</param>
    /// <param name="minimumLength">The minimum required length of the array.</param>
    /// <remarks>
    /// The actual length of the array may be greater than <paramref name="minimumLength"/>.
    /// </remarks>
    public RentedArray(ArrayPool<T> pool, int minimumLength)
    {
        _pool = pool;
        Array = _pool.Rent(minimumLength);
    }

    /// <summary>
    /// Returns the array to its originating pool.
    /// </summary>
    public void Dispose() => _pool.Return(Array);
}