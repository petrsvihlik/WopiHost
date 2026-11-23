using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// Provides the shared fixture for the mutating endpoint test classes
/// (<see cref="WopiHost.IntegrationTests.FileMutatingEndpointTests"/> and
/// <see cref="WopiHost.IntegrationTests.ContainerMutatingEndpointTests"/>), backed by a
/// per-collection copy of <c>sample/wopi-docs</c>.
/// </summary>
/// <remarks>
/// <para>
/// Mutating tests need an isolated file-system root so they don't corrupt the shared
/// <c>sample/wopi-docs</c> that read-only tests rely on, so this fixture copies the sample into a
/// temp directory, points <see cref="WopiBackendFactory"/> at it, and deletes it in
/// <see cref="Dispose"/>.
/// </para>
/// <para>
/// Both mutating test classes share one backend boot via
/// <see cref="MutatingEndpointsCollection"/>. The fixture creates files / containers via the
/// writable provider so the FileSystemProvider's id↔path cache stays in sync (dropping raw
/// bytes on disk would leave the cache stale and the file invisible to <c>GetWopiFiles</c>).
/// File and container tests don't conflict at runtime because all temp names are GUID-suffixed.
/// </para>
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
    /// so the FileSystemProvider's id↔path cache registers the new identifier, then writes
    /// <paramref name="contents"/> through the writable file handle.
    /// </summary>
    /// <param name="contents">The bytes to write into the new file.</param>
    /// <param name="extension">The file extension, including the leading dot.</param>
    /// <returns>The new file's identifier.</returns>
    public async Task<string> CreateTempFileAsync(byte[] contents, string extension = ".bin")
        => (await CreateTempFileWithPathAsync(contents, extension)).FileId;

    /// <summary>
    /// Creates a file like <see cref="CreateTempFileAsync"/>, and also reports its on-disk path so a
    /// test can mutate the file behind the provider's back (stale id↔path binding scenarios).
    /// </summary>
    /// <param name="contents">The bytes to write into the new file.</param>
    /// <param name="extension">The file extension, including the leading dot.</param>
    /// <returns>The new file's identifier and its on-disk path.</returns>
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
    /// so the id↔path cache registers it.
    /// </summary>
    /// <returns>The new container's identifier.</returns>
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
/// Defines the xUnit collection through which the file and container mutating test classes share one
/// <see cref="MutatingEndpointsFixture"/> instance, so the backend boots once instead of per class.
/// </summary>
[CollectionDefinition("MutatingEndpoints")]
public sealed class MutatingEndpointsCollection : ICollectionFixture<MutatingEndpointsFixture>
{
}
