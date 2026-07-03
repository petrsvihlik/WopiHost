using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// Shared fixture for the mutating endpoint test classes
/// (<see cref="WopiHost.IntegrationTests.FileMutatingEndpointTests"/> +
/// <see cref="WopiHost.IntegrationTests.ContainerMutatingEndpointTests"/>). Mutating tests
/// need an isolated file-system root so they don't corrupt the shared sample/wopi-docs that
/// read-only tests rely on — this fixture copies the sample into a per-collection temp
/// directory and points <see cref="WopiBackendFactory"/> at it. Cleanup happens in
/// <see cref="IDisposable.Dispose"/>.
/// </summary>
/// <remarks>
/// Both mutating test classes share one backend boot via
/// <see cref="MutatingEndpointsCollection"/>. The fixture creates files / containers via the
/// writable provider so the FileSystemProvider's id↔path cache stays in sync (dropping raw
/// bytes on disk would leave the cache stale and the file invisible to <c>GetWopiFiles</c>).
/// File / container tests don't conflict at runtime — all temp names are GUID-prefixed.
/// </remarks>
public sealed class MutatingEndpointsFixture : IDisposable
{
    private const string SharedSigningSecret = "mutating-tests-shared-key-32bytes!";
    private static readonly FixtureUser s_user = new("mut-user", "Mut User", "mut@example.com");

    private readonly string _tempRoot;
    public WopiBackendFactory WopiBackend { get; }
    public string RootContainerId { get; }

    public MutatingEndpointsFixture()
    {
        _tempRoot = Path.Join(Path.GetTempPath(), $"wopi-mut-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        CopyDirectory(TestPaths.WopiDocsRoot, _tempRoot);

        WopiBackend = new WopiBackendFactory(SharedSigningSecret, storageRootPath: _tempRoot);

        using var scope = WopiBackend.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>();
        RootContainerId = storage.RootContainer.Identifier;
    }

    public Task<string> MintFileTokenAsync(string fileId, WopiFilePermissions permissions = WopiFilePermissions.UserCanWrite | WopiFilePermissions.UserCanRename)
        => FixtureTokens.MintFileTokenAsync(WopiBackend, s_user, fileId, permissions);

    public Task<string> MintContainerTokenAsync(string containerId)
        => FixtureTokens.MintContainerTokenAsync(WopiBackend, s_user, containerId);

    /// <summary>
    /// Creates a file via <see cref="IWopiWritableStorageProvider.CreateWopiChildFile"/>
    /// so the FileSystemProvider's id→path cache registers the new identifier. Then writes
    /// <paramref name="contents"/> through the writable file handle.
    /// </summary>
    public async Task<string> CreateTempFileAsync(byte[] contents, string extension = ".bin")
        => (await CreateTempFileWithPathAsync(contents, extension)).FileId;

    /// <summary>
    /// Like <see cref="CreateTempFileAsync"/>, but also returns the on-disk path so a test can
    /// mutate the file behind the provider's back (stale id→path binding scenarios).
    /// </summary>
    public async Task<(string FileId, string DiskPath)> CreateTempFileWithPathAsync(byte[] contents, string extension = ".bin")
    {
        var fileName = $"mut-{Guid.NewGuid():N}{extension}";
        using var scope = WopiBackend.Services.CreateScope();
        var writable = scope.ServiceProvider.GetRequiredService<IWopiWritableStorageProvider>();
        var file = await writable.CreateWopiChildFile(RootContainerId, fileName)
            ?? throw new InvalidOperationException($"CreateWopiChildFile returned null for {fileName}");
        if (contents.Length > 0)
        {
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(contents);
        }
        return (file.Identifier, Path.Join(_tempRoot, fileName));
    }

    /// <summary>
    /// Creates a subfolder via <see cref="IWopiWritableStorageProvider.CreateWopiChildContainer"/>
    /// so the id↔path cache registers it. Returns the new container's identifier.
    /// </summary>
    public async Task<string> CreateTempContainerAsync()
    {
        var folderName = $"mut-container-{Guid.NewGuid():N}";
        using var scope = WopiBackend.Services.CreateScope();
        var writable = scope.ServiceProvider.GetRequiredService<IWopiWritableStorageProvider>();
        var container = await writable.CreateWopiChildContainer(RootContainerId, folderName)
            ?? throw new InvalidOperationException($"CreateWopiChildContainer returned null for {folderName}");
        return container.Identifier;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(source, destination, StringComparison.Ordinal));
        }
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, destination, StringComparison.Ordinal), overwrite: true);
        }
    }

    public void Dispose()
    {
        WopiBackend.Dispose();
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort cleanup */ }
    }
}

/// <summary>
/// xUnit collection wrapper so the file + container mutating test classes share one
/// <see cref="MutatingEndpointsFixture"/> instance. Without the collection, each class
/// would boot its own backend (acceptable but ~3-5s of redundant startup overhead per class).
/// </summary>
[CollectionDefinition("MutatingEndpoints")]
public sealed class MutatingEndpointsCollection : ICollectionFixture<MutatingEndpointsFixture>
{
}
