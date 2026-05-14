namespace WopiHost.Abstractions.Testing;

/// <summary>
/// Minimal manually-driven <see cref="TimeProvider"/> that lets tests advance wall-clock time
/// without sleeping. Equivalent to <c>FakeTimeProvider</c> from
/// <c>Microsoft.Extensions.TimeProvider.Testing</c>, kept here so the conformance library
/// doesn't drag the extra package onto every consumer.
/// </summary>
/// <param name="start">Initial UTC time the provider returns from <see cref="GetUtcNow"/>.</param>
public sealed class ControllableTimeProvider(DateTimeOffset start) : TimeProvider
{
    /// <summary>Current "now"; mutate to advance the clock.</summary>
    public DateTimeOffset Now { get; set; } = start;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Now;
}
