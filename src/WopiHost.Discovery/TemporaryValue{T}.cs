namespace WopiHost.Discovery;

/// <summary>
/// Represents a value that expires at a given point in time.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public struct TemporaryValue<T>
{
    /// <summary>
    /// Gets or sets the value, which expires at <see cref="ValidUntil"/>.
    /// </summary>
    public T Result { get; set; }

    /// <summary>
    /// Gets or sets the point in time until which <see cref="Result"/> remains valid.
    /// </summary>
    public DateTimeOffset ValidUntil { get; set; }
}
