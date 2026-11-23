namespace WopiHost.Discovery;

/// <summary>
/// Wraps a lazily created value in an expiring envelope.
/// </summary>
/// <typeparam name="T">The type of the temporary value.</typeparam>
/// <remarks>
/// Based on <see href="https://github.com/filipw/async-expiring-lazy">async-expiring-lazy</see>
/// (see <see href="https://www.strathweb.com/2016/11/lazy-async-initialization-for-expiring-objects/">Lazy async initialization for expiring objects</see>).
/// </remarks>
/// <param name="valueProvider">A delegate that creates the value, given the previous (possibly expired) one.</param>
/// <exception cref="ArgumentNullException"><paramref name="valueProvider"/> is <see langword="null"/>.</exception>
public class AsyncExpiringLazy<T>(Func<TemporaryValue<T>, Task<TemporaryValue<T>>> valueProvider) : IDisposable
{
    // Instance-scoped on purpose: a static lock would be shared across every
    // AsyncExpiringLazy<T> with the same closed generic type, serializing
    // unrelated callers.
    private readonly SemaphoreSlim _syncLock = new(initialCount: 1);
    private readonly Func<TemporaryValue<T>, Task<TemporaryValue<T>>> _valueProvider = valueProvider ?? throw new ArgumentNullException(nameof(valueProvider));
    private TemporaryValue<T> _value;
    private bool _disposed;
    private bool IsValueCreatedInternal => _value.Result != null && _value.ValidUntil > DateTimeOffset.UtcNow;

    /// <summary>
    /// Determines whether a value has been created and is still valid.
    /// </summary>
    /// <returns><see langword="true"/> if a non-<see langword="null"/> value exists and has not expired; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> IsValueCreated()
    {
        await _syncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return IsValueCreatedInternal;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Gets the current value, creating a new one if none exists or it has expired.
    /// </summary>
    /// <returns>The current value.</returns>
    public async Task<T> Value()
    {
        // Hold the lock for the entire operation so concurrent first-time
        // callers do not each invoke the (typically network-bound) value
        // provider. Releasing between the cache check and the fetch would let
        // multiple callers fetch concurrently.
        await _syncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsValueCreatedInternal)
            {
                return _value.Result;
            }

            var result = await _valueProvider(_value).ConfigureAwait(false);
            _value = result;
            return _value.Result;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Invalidates the current value causing a new value to be created when <see cref="Value"/> is called next time.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Invalidate()
    {
        await _syncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _value = default;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Releases the internal <see cref="SemaphoreSlim"/>. Derived types that hold additional
    /// resources override <see cref="Dispose(bool)"/>.
    /// </remarks>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources held by this instance.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }
        if (disposing)
        {
            _syncLock.Dispose();
        }
        _disposed = true;
    }
}
