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
public class AsyncExpiringLazy<T>(Func<TemporaryValue<T>, Task<TemporaryValue<T>>> valueProvider)
{
    private static readonly SemaphoreSlim SyncLock = new(initialCount: 1);
    private readonly Func<TemporaryValue<T>, Task<TemporaryValue<T>>> _valueProvider = valueProvider ?? throw new ArgumentNullException(nameof(valueProvider));
    private TemporaryValue<T> _value;
    private bool IsValueCreatedInternal => _value.Result != null && _value.ValidUntil > DateTimeOffset.UtcNow;

    /// <summary>
    /// Returns true if a value has been created and is still valid.
    /// </summary>
    public async Task<bool> IsValueCreated()
    {
        await SyncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return IsValueCreatedInternal;
        }
        finally
        {
            SyncLock.Release();
        }
    }

    /// <summary>
    /// Gets the current value or creates a new one, if expired.
    /// </summary>
    public async Task<T> Value()
    {
        await SyncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsValueCreatedInternal)
            {
                return _value.Result;
            }
        }
        finally
        {
            SyncLock.Release();
        }

        await SyncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var result = await _valueProvider(_value).ConfigureAwait(false);
            _value = result;
            return _value.Result;
        }
        finally
        {
            SyncLock.Release();
        }
    }

    /// <summary>
    /// Invalidates the current value causing a new value to be created when <see cref="Value"/> is called next time.
    /// </summary>
    public async Task Invalidate()
    {
        await SyncLock.WaitAsync().ConfigureAwait(false);
        _value = default;
        SyncLock.Release();
    }
}
