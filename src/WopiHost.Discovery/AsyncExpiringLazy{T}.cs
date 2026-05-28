namespace WopiHost.Discovery;

/// <summary>
/// Wraps a type in an expiring envelope.
/// Kudos to: https://github.com/filipw/async-expiring-lazy
/// https://www.strathweb.com/2016/11/lazy-async-initialization-for-expiring-objects/
/// </summary>
/// <typeparam name="T">Type of the temporary value.</typeparam>
/// <remarks>
/// Creates a new instance of <see cref="AsyncExpiringLazy{T}"/>.
/// </remarks>
/// <param name="valueProvider">A delegate that facilitates the creation of the value.</param>
/// <exception cref="ArgumentNullException">The <paramref name="valueProvider"/> must be initialized.</exception>
public class AsyncExpiringLazy<T>(Func<TemporaryValue<T>, Task<TemporaryValue<T>>> valueProvider) : IDisposable
{
    // Instance-scoped on purpose: a static lock would be shared across every
    // AsyncExpiringLazy<T> with the same closed generic type, serializing
    // unrelated callers (also fixed in #303).
    private readonly SemaphoreSlim _syncLock = new(initialCount: 1);
    private readonly Func<TemporaryValue<T>, Task<TemporaryValue<T>>> _valueProvider = valueProvider ?? throw new ArgumentNullException(nameof(valueProvider));
    private TemporaryValue<T> _value;
    private bool _disposed;
    private bool IsValueCreatedInternal => _value.Result != null && _value.ValidUntil > DateTimeOffset.UtcNow;

    /// <summary>
    /// Returns true if a value has been created and is still valid.
    /// </summary>
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
    /// Gets the current value or creates a new one, if expired.
    /// </summary>
    public async Task<T> Value()
    {
        // Hold the lock for the entire operation so concurrent first-time
        // callers do not each invoke the (typically network-bound) value
        // provider. Releasing between the cache check and the fetch was the
        // bug fixed in #303.
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
    public async Task Invalidate()
    {
        await _syncLock.WaitAsync().ConfigureAwait(false);
        _value = default;
        _syncLock.Release();
    }

    /// <summary>
    /// Releases the internal <see cref="SemaphoreSlim"/>. The class is not
    /// sealed; derived types should override <see cref="Dispose(bool)"/> if
    /// they hold additional resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources held by this instance.
    /// </summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
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
