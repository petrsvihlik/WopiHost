using System.Collections.ObjectModel;
using System.Net;
using WopiHost.Abstractions;
using WopiHost.FileSystemProvider;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// Drives the vanish-race branches of the read endpoints: the file passes the up-front
/// <c>Exists</c> guard but the content read throws <see cref="FileNotFoundException"/> — a file
/// deleted between the check and the open. The decorated storage provider makes that
/// otherwise-unreachable race deterministic; both endpoints must answer 404, not 500.
/// </summary>
public sealed class VanishRaceTests : IDisposable
{
    private const string SigningSecret = "vanish-race-tests-shared-key-32b!";
    private static readonly FixtureUser s_user = new("vanish-user", "Vanish User", "vanish@example.com");

    private readonly WopiBackendFactory _backend;

    public VanishRaceTests()
    {
        _backend = new WopiBackendFactory(SigningSecret, configureServices: services =>
        {
            // Replace the forwarding IWopiStorageProvider registration with a decorator over the
            // same WopiFileSystemProvider singleton — id resolution and metadata stay real, only
            // content reads fault.
            services.RemoveAll<IWopiStorageProvider>();
            services.AddSingleton<IWopiStorageProvider>(sp =>
                new VanishingReadStorage(sp.GetRequiredService<WopiFileSystemProvider>()));
        });
    }

    public void Dispose() => _backend.Dispose();

    private async Task<(string FileId, string Token)> ResolveAnyFileAsync()
    {
        using var scope = _backend.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>();
        string? fileId = null;
        await foreach (var file in storage.GetWopiFiles(storage.RootContainer.Identifier))
        {
            fileId = file.Identifier;
            break;
        }
        Assert.NotNull(fileId);
        var token = await FixtureTokens.MintFileTokenAsync(
            _backend, s_user, fileId, WopiFilePermissions.UserCanWrite | WopiFilePermissions.UserCanRename);
        return (fileId, token);
    }

    [Fact]
    public async Task CheckFileInfo_FileVanishesAfterExistsCheck_Returns_404()
    {
        var (fileId, token) = await ResolveAnyFileAsync();
        using var client = _backend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetFile_FileVanishesAfterExistsCheck_Returns_404()
    {
        var (fileId, token) = await ResolveAnyFileAsync();
        using var client = _backend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{fileId}/contents?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}

/// <summary>Delegates everything to the real provider but wraps files in <see cref="VanishedFile"/>.</summary>
file sealed class VanishingReadStorage(IWopiStorageProvider inner) : IWopiStorageProvider
{
    public IWopiContainer RootContainer => inner.RootContainer;

    public async Task<IWopiFile?> GetWopiFile(string identifier, CancellationToken cancellationToken = default)
        => await inner.GetWopiFile(identifier, cancellationToken) is { } file ? new VanishedFile(file) : null;

    public Task<IWopiContainer?> GetWopiContainer(string identifier, CancellationToken cancellationToken = default)
        => inner.GetWopiContainer(identifier, cancellationToken);

    public IAsyncEnumerable<IWopiFile> GetWopiFiles(string identifier, IReadOnlyCollection<string>? fileExtensions = null, CancellationToken cancellationToken = default)
        => inner.GetWopiFiles(identifier, fileExtensions, cancellationToken);

    public IAsyncEnumerable<IWopiContainer> GetWopiContainers(string identifier, CancellationToken cancellationToken = default)
        => inner.GetWopiContainers(identifier, cancellationToken);

    public Task<ReadOnlyCollection<IWopiContainer>> GetFileAncestors(string fileId, CancellationToken cancellationToken = default)
        => inner.GetFileAncestors(fileId, cancellationToken);

    public Task<ReadOnlyCollection<IWopiContainer>> GetContainerAncestors(string containerId, CancellationToken cancellationToken = default)
        => inner.GetContainerAncestors(containerId, cancellationToken);

    public Task<IWopiFile?> GetWopiFileByName(string containerId, string name, CancellationToken cancellationToken = default)
        => inner.GetWopiFileByName(containerId, name, cancellationToken);

    public Task<IWopiContainer?> GetWopiContainerByName(string containerId, string name, CancellationToken cancellationToken = default)
        => inner.GetWopiContainerByName(containerId, name, cancellationToken);
}

/// <summary>A file that reports itself present but whose content read faults — the vanish race.</summary>
file sealed class VanishedFile(IWopiFile inner) : IWopiFile
{
    public string Name => inner.Name;
    public string Identifier => inner.Identifier;
    public string Owner => inner.Owner;
    public bool Exists => inner.Exists;
    public long Length => inner.Length;
    public DateTime LastWriteTimeUtc => inner.LastWriteTimeUtc;
    public string Extension => inner.Extension;
    public string? Version => inner.Version;
    public ReadOnlyMemory<byte>? Checksum => inner.Checksum;

    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        => throw new FileNotFoundException("Simulated vanish between the Exists check and the open.");
}
