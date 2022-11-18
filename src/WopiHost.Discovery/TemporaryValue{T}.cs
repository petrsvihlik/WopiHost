namespace WopiHost.Discovery;

/// <summary>
/// Represents a value that's about to expire.
/// </summary>
/// <typeparam name="T">Type of the value that's about to expire.</typeparam>
public struct TemporaryValue<T>
{
    /// <summary>
    /// The value of the <see cref="AsyncExpiringLazy{T}"/> that's about to expire at <see cref="ValidUntil"/>.
    /// </summary>
    public T Result { get; set; }

    /// <summary>
    /// Determines how long the <see cref="Result"/> remains valid.
    /// </summary>
    public DateTimeOffset ValidUntil { get; set; }
}
