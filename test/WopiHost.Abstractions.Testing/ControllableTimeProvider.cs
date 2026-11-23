namespace WopiHost.Abstractions.Testing;

/// <summary>
/// Represents a manually driven <see cref="TimeProvider"/> that lets tests advance wall-clock time
/// without sleeping.
/// </summary>
/// <remarks>
/// Equivalent to <c>FakeTimeProvider</c> from <c>Microsoft.Extensions.TimeProvider.Testing</c>;
/// defined here so consumers of the conformance library take no dependency on that package.
/// </remarks>
/// <param name="start">The initial UTC time that <see cref="GetUtcNow"/> returns.</param>
public sealed class ControllableTimeProvider(DateTimeOffset start) : TimeProvider
{
    /// <summary>Gets or sets the current UTC time that <see cref="GetUtcNow"/> returns; set it to advance the clock.</summary>
    public DateTimeOffset Now { get; set; } = start;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Now;
}
