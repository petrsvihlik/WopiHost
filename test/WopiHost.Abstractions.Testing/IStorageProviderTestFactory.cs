namespace WopiHost.Abstractions.Testing;

/// <summary>
/// Represents a storage provider under test plus the seam used to seed it.
/// </summary>
/// <remarks>
/// The reader and writer are the same instance for the in-tree providers (one type implements both
/// interfaces); they are split so the conformance suite reads through <see cref="IWopiStorageProvider"/>
/// and seeds through <see cref="IWopiWritableStorageProvider"/>.
/// </remarks>
public interface IStorageProviderTestContext : IAsyncDisposable
{
    /// <summary>Gets the read side under test.</summary>
    IWopiStorageProvider Reader { get; }

    /// <summary>Gets the write side, used only to seed fixtures.</summary>
    IWopiWritableStorageProvider Writer { get; }
}

/// <summary>
/// Provides the default <see cref="IStorageProviderTestContext"/>, which holds the reader and writer
/// and runs an optional teardown (deleting the temp directory, dropping the blob container) on disposal.
/// </summary>
/// <param name="reader">The read side under test.</param>
/// <param name="writer">The write side used to seed fixtures.</param>
/// <param name="onDisposeAsync">The teardown to run on disposal, or <see langword="null"/> for none.</param>
public sealed class StorageProviderTestContext(
    IWopiStorageProvider reader,
    IWopiWritableStorageProvider writer,
    Func<ValueTask>? onDisposeAsync = null) : IStorageProviderTestContext
{
    /// <inheritdoc/>
    public IWopiStorageProvider Reader { get; } = reader;

    /// <inheritdoc/>
    public IWopiWritableStorageProvider Writer { get; } = writer;

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => onDisposeAsync?.Invoke() ?? ValueTask.CompletedTask;
}

/// <summary>
/// Creates a fresh storage provider for each test of <see cref="StorageProviderConformanceTests"/>.
/// </summary>
/// <remarks>
/// A provider's test project derives a sealed subclass of <see cref="StorageProviderConformanceTests"/>
/// and returns an implementation of this interface from <see cref="StorageProviderConformanceTests.Factory"/>.
/// Each call must return a provider rooted on storage that is logically <em>independent</em> of
/// any earlier one (a fresh temp directory for the file system, a unique blob container for
/// Azure) and whose root starts empty — every test seeds exactly what it asserts through the
/// writer.
/// </remarks>
public interface IStorageProviderTestFactory
{
    /// <summary>Creates a fresh provider under test with an empty root.</summary>
    /// <returns>The context holding the provider under test.</returns>
    Task<IStorageProviderTestContext> CreateAsync();
}
