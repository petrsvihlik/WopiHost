namespace WopiHost.Abstractions.Testing;

/// <summary>
/// A storage provider under test plus the seam used to seed it. The reader and writer are the
/// same provider instance for the in-tree providers (both interfaces are implemented on one
/// type); they are split here so the conformance suite reads through <see cref="IWopiStorageProvider"/>
/// and seeds through <see cref="IWopiWritableStorageProvider"/>.
/// </summary>
public interface IStorageProviderTestContext : IAsyncDisposable
{
    /// <summary>Read side under test.</summary>
    IWopiStorageProvider Reader { get; }

    /// <summary>Write side used only to seed fixtures.</summary>
    IWopiWritableStorageProvider Writer { get; }
}

/// <summary>
/// Default <see cref="IStorageProviderTestContext"/>: holds the reader/writer and runs an
/// optional teardown (delete the temp directory, drop the blob container, …) on disposal.
/// </summary>
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
/// Factory the storage conformance suite uses to obtain a fresh provider for each test. Derive a
/// concrete sealed subclass of <see cref="StorageProviderConformanceTests"/> in each provider's
/// test project and override its <c>Factory</c> to return one of these.
/// </summary>
/// <remarks>
/// Each call must return a provider rooted on storage that is logically <em>independent</em> of
/// any earlier one (a fresh temp directory for the file system, a unique blob container for
/// Azure) and whose root starts empty — every test seeds exactly what it asserts through the
/// writer.
/// </remarks>
public interface IStorageProviderTestFactory
{
    /// <summary>Create a fresh provider under test with an empty root.</summary>
    Task<IStorageProviderTestContext> CreateAsync();
}
