using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

[Trait("Category", "Integration")]
[Collection(AzuriteCollection.Name)]
public class WopiAzureStorageProviderTests(AzuriteFixture azurite)
{
    private async Task<(WopiAzureStorageProvider provider, BlobContainerClient container)> CreateProviderAsync()
    {
        // Per-test container so tests don't interfere with each other.
        var serviceClient = azurite.CreateBlobServiceClient();
        var container = serviceClient.GetBlobContainerClient($"wopi-test-{Guid.NewGuid():N}");
        var idMap = new BlobIdMap(NullLogger<BlobIdMap>.Instance);
        var provider = new WopiAzureStorageProvider(container, idMap, NullLogger<WopiAzureStorageProvider>.Instance);

        // Force initialization (creates the container, scans the empty space).
        _ = await provider.GetWopiContainer(provider.RootContainer.Identifier);
        return (provider, container);
    }

    private static async Task UploadAsync(BlobContainerClient container, string path, string content)
    {
        var blob = container.GetBlobClient(path);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await blob.UploadAsync(stream, overwrite: true);
    }

    [Fact]
    public async Task GetWopiResource_File_ReturnsFile_WhenBlobExists()
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "hello.txt", "world");

        // Re-list so the id map picks up the seeded blob.
        var files = new List<IWopiFile>();
        await foreach (var f in provider.GetWopiFiles(provider.RootContainer.Identifier, cancellationToken: TestContext.Current.CancellationToken))
        {
            files.Add(f);
        }
        Assert.Single(files);

        var fetched = await provider.GetWopiFile(files[0].Identifier, TestContext.Current.CancellationToken);
        Assert.NotNull(fetched);
        Assert.True(fetched.Exists);
        Assert.Equal("hello", fetched.Name);
        Assert.Equal("txt", fetched.Extension);
        Assert.Equal(5, fetched.Length);
        Assert.NotNull(fetched.Version);
    }

    [Fact]
    public async Task GetWopiResource_UnknownIdentifier_ReturnsNull()
    {
        var (provider, _) = await CreateProviderAsync();
        var fetched = await provider.GetWopiFile("nonexistent", TestContext.Current.CancellationToken);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsBlobContent()
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "doc.txt", "stream-me");
        await foreach (var f in provider.GetWopiFiles(provider.RootContainer.Identifier, cancellationToken: TestContext.Current.CancellationToken)) { _ = f; }
        var file = await provider.GetWopiFileByName(provider.RootContainer.Identifier, "doc.txt", TestContext.Current.CancellationToken);

        Assert.NotNull(file);
        await using var s = await file.OpenReadAsync(TestContext.Current.CancellationToken);
        using var reader = new StreamReader(s);
        Assert.Equal("stream-me", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OpenWriteAsync_StoresContent_AndComputesSha256()
    {
        var (provider, _) = await CreateProviderAsync();
        var created = await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "writeable.txt", TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.True(created.Exists);

        const string payload = "the quick brown fox";
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var writable = (await provider.GetWritableFile(created.Identifier, TestContext.Current.CancellationToken))!;
        await using (var s = await writable.OpenWriteAsync(TestContext.Current.CancellationToken))
        {
            await s.WriteAsync(System.Text.Encoding.UTF8.GetBytes(payload), TestContext.Current.CancellationToken);
        }

        // Re-fetch so cached metadata reflects the new state.
        var refreshed = (await provider.GetWopiFile(created.Identifier, TestContext.Current.CancellationToken))!;
        Assert.Equal(payload.Length, refreshed.Length);
        Assert.NotNull(refreshed.Checksum);
        Assert.Equal(expectedHash, Convert.ToHexString(refreshed.Checksum.Value.Span).ToLowerInvariant());
    }

    [Fact]
    public async Task CreateWopiChildResource_File_AddsZeroByteBlob()
    {
        var (provider, _) = await CreateProviderAsync();

        var created = await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "fresh.docx", TestContext.Current.CancellationToken);

        Assert.NotNull(created);
        Assert.True(created.Exists);
        Assert.Equal(0, created.Length);
        Assert.Equal("fresh", created.Name);
        Assert.Equal("docx", created.Extension);
    }

    [Fact]
    public async Task CreateWopiChildResource_File_DuplicateName_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "dup.txt", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CreateWopiChildFile(provider.RootContainer.Identifier, "dup.txt", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateWopiChildResource_Folder_MaterializesMarkerBlob_AndIsListable()
    {
        var (provider, _) = await CreateProviderAsync();

        var folder = await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "subfolder", TestContext.Current.CancellationToken);

        Assert.NotNull(folder);
        Assert.Equal("subfolder", folder.Name);

        var folders = new List<IWopiContainer>();
        await foreach (var f in provider.GetWopiContainers(provider.RootContainer.Identifier, TestContext.Current.CancellationToken))
        {
            folders.Add(f);
        }
        Assert.Contains(folders, f => f.Name == "subfolder");
    }

    [Fact]
    public async Task GetWopiFiles_HidesFolderMarker()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "with-marker", TestContext.Current.CancellationToken);

        var files = new List<IWopiFile>();
        await foreach (var f in provider.GetWopiFiles(folder!.Identifier, cancellationToken: TestContext.Current.CancellationToken))
        {
            files.Add(f);
        }
        Assert.Empty(files);
    }

    [Fact]
    public async Task DeleteWopiResource_File_RemovesBlob_AndDropsId()
    {
        var (provider, _) = await CreateProviderAsync();
        var created = (await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "doomed.txt", TestContext.Current.CancellationToken))!;

        var deleted = await provider.DeleteWopiFile(created.Identifier, TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.Null(await provider.GetWopiFile(created.Identifier, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteWopiResource_Folder_NonEmpty_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "with-content", TestContext.Current.CancellationToken))!;
        _ = await provider.CreateWopiChildFile(folder.Identifier, "child.txt", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.DeleteWopiContainer(folder.Identifier, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteWopiResource_Folder_Empty_Succeeds()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "ephemeral", TestContext.Current.CancellationToken))!;

        var deleted = await provider.DeleteWopiContainer(folder.Identifier, TestContext.Current.CancellationToken);

        Assert.True(deleted);
    }

    [Fact]
    public async Task RenameWopiResource_File_PreservesIdentifier()
    {
        var (provider, _) = await CreateProviderAsync();
        var created = (await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "before.txt", TestContext.Current.CancellationToken))!;
        var originalId = created.Identifier;

        var renamed = await provider.RenameWopiFile(originalId, "after.txt", TestContext.Current.CancellationToken);

        Assert.True(renamed);
        var fetched = await provider.GetWopiFile(originalId, TestContext.Current.CancellationToken);
        Assert.NotNull(fetched);
        Assert.Equal("after", fetched.Name);
    }

    [Fact]
    public async Task RenameWopiResource_File_TargetExists_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        var src = (await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "source.txt", TestContext.Current.CancellationToken))!;
        _ = await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "target.txt", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.RenameWopiFile(src.Identifier, "target.txt", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSuggestedName_File_ReturnsCounterSuffix_WhenNameExists()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "report.docx", TestContext.Current.CancellationToken);

        var suggested = await provider.GetSuggestedFileName(
            provider.RootContainer.Identifier, "report.docx", TestContext.Current.CancellationToken);

        Assert.Equal("report (1).docx", suggested);
    }

    [Fact]
    public async Task GetSuggestedName_File_ReturnsName_WhenNameAvailable()
    {
        var (provider, _) = await CreateProviderAsync();

        var suggested = await provider.GetSuggestedFileName(
            provider.RootContainer.Identifier, "fresh.docx", TestContext.Current.CancellationToken);

        Assert.Equal("fresh.docx", suggested);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("")]
    [InlineData(".wopi.folder")]
    [InlineData("..")]
    public async Task CheckValidName_RejectsIllegalNames(string name)
    {
        var (provider, _) = await CreateProviderAsync();

        Assert.False(await provider.CheckValidFileName(name, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckValidName_AcceptsTypicalName()
    {
        var (provider, _) = await CreateProviderAsync();

        Assert.True(await provider.CheckValidFileName("my-file_2.docx", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("a/b.txt")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData(BlobIdMap.FolderMarker)]
    public async Task CreateWopiChildFile_TraversalOrReservedName_Throws(string name)
    {
        // The name is client-controlled (relative/suggested target); a name that isn't a single
        // path segment must be rejected before it's composed into a blob path so it can't escape
        // the container or collide with the reserved folder marker. The predicate is covered by
        // CheckValidName_RejectsIllegalNames; this pins that the create path actually calls it.
        var (provider, _) = await CreateProviderAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CreateWopiChildFile(provider.RootContainer.Identifier, name, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData(BlobIdMap.FolderMarker)]
    public async Task CreateWopiChildContainer_TraversalOrReservedName_Throws(string name)
    {
        var (provider, _) = await CreateProviderAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CreateWopiChildContainer(provider.RootContainer.Identifier, name, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAncestors_File_IncludesParentChain()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "outer", TestContext.Current.CancellationToken))!;
        var inner = (await provider.CreateWopiChildContainer(folder.Identifier, "inner", TestContext.Current.CancellationToken))!;
        var file = (await provider.CreateWopiChildFile(inner.Identifier, "deep.txt", TestContext.Current.CancellationToken))!;

        var ancestors = await provider.GetFileAncestors(file.Identifier, TestContext.Current.CancellationToken);

        // Expected: [root, outer, inner]
        Assert.Equal(3, ancestors.Count);
        Assert.Equal(provider.RootContainer.Identifier, ancestors[0].Identifier);
        Assert.Equal("outer", ancestors[1].Name);
        Assert.Equal("inner", ancestors[2].Name);
    }
}
