using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

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
        await foreach (var f in provider.GetWopiFiles(provider.RootContainer.Identifier))
        {
            files.Add(f);
        }
        Assert.Single(files);

        var fetched = await provider.GetWopiFile(files[0].Identifier);
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
        var fetched = await provider.GetWopiFile("nonexistent");
        Assert.Null(fetched);
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsBlobContent()
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "doc.txt", "stream-me");
        await foreach (var f in provider.GetWopiFiles(provider.RootContainer.Identifier)) { _ = f; }
        var file = await provider.GetWopiFileByName(provider.RootContainer.Identifier, "doc.txt");

        Assert.NotNull(file);
        await using var s = await file.OpenReadAsync();
        using var reader = new StreamReader(s);
        Assert.Equal("stream-me", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task OpenWriteAsync_StoresContent_AndComputesSha256()
    {
        var (provider, _) = await CreateProviderAsync();
        var created = await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "writeable.txt");
        Assert.NotNull(created);
        Assert.True(created.Exists);

        const string payload = "the quick brown fox";
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var writable = (await provider.GetWopiFile(created.Identifier))!;
        await using (var s = await writable.OpenWriteAsync())
        {
            await s.WriteAsync(System.Text.Encoding.UTF8.GetBytes(payload));
        }

        // Re-fetch so cached metadata reflects the new state.
        var refreshed = (await provider.GetWopiFile(created.Identifier))!;
        Assert.Equal(payload.Length, refreshed.Length);
        Assert.NotNull(refreshed.Checksum);
        Assert.Equal(expectedHash, Convert.ToHexString(refreshed.Checksum.Value.Span).ToLowerInvariant());
    }

    [Fact]
    public async Task CreateWopiChildResource_File_AddsZeroByteBlob()
    {
        var (provider, _) = await CreateProviderAsync();

        var created = await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "fresh.docx");

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
        _ = await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "dup.txt");

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CreateWopiChildFile(provider.RootContainer.Identifier, "dup.txt"));
    }

    [Fact]
    public async Task CreateWopiChildResource_Folder_MaterializesMarkerBlob_AndIsListable()
    {
        var (provider, _) = await CreateProviderAsync();

        var folder = await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "subfolder");

        Assert.NotNull(folder);
        Assert.Equal("subfolder", folder.Name);

        var folders = new List<IWopiContainer>();
        await foreach (var f in provider.GetWopiContainers(provider.RootContainer.Identifier))
        {
            folders.Add(f);
        }
        Assert.Contains(folders, f => f.Name == "subfolder");
    }

    [Fact]
    public async Task GetWopiFiles_HidesFolderMarker()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "with-marker");

        var files = new List<IWopiFile>();
        await foreach (var f in provider.GetWopiFiles(folder!.Identifier))
        {
            files.Add(f);
        }
        Assert.Empty(files);
    }

    [Fact]
    public async Task DeleteWopiResource_File_RemovesBlob_AndDropsId()
    {
        var (provider, _) = await CreateProviderAsync();
        var created = (await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "doomed.txt"))!;

        var deleted = await provider.DeleteWopiFile(created.Identifier);

        Assert.True(deleted);
        Assert.Null(await provider.GetWopiFile(created.Identifier));
    }

    [Fact]
    public async Task DeleteWopiResource_Folder_NonEmpty_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "with-content"))!;
        _ = await provider.CreateWopiChildFile(folder.Identifier, "child.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.DeleteWopiContainer(folder.Identifier));
    }

    [Fact]
    public async Task DeleteWopiResource_Folder_Empty_Succeeds()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "ephemeral"))!;

        var deleted = await provider.DeleteWopiContainer(folder.Identifier);

        Assert.True(deleted);
    }

    [Fact]
    public async Task RenameWopiResource_File_PreservesIdentifier()
    {
        var (provider, _) = await CreateProviderAsync();
        var created = (await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "before.txt"))!;
        var originalId = created.Identifier;

        var renamed = await provider.RenameWopiFile(originalId, "after.txt");

        Assert.True(renamed);
        var fetched = await provider.GetWopiFile(originalId);
        Assert.NotNull(fetched);
        Assert.Equal("after", fetched.Name);
    }

    [Fact]
    public async Task RenameWopiResource_File_TargetExists_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        var src = (await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "source.txt"))!;
        _ = await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "target.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.RenameWopiFile(src.Identifier, "target.txt"));
    }

    [Fact]
    public async Task GetSuggestedName_File_ReturnsCounterSuffix_WhenNameExists()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "report.docx");

        var suggested = await provider.GetSuggestedFileName(
            provider.RootContainer.Identifier, "report.docx");

        Assert.Equal("report (1).docx", suggested);
    }

    [Fact]
    public async Task GetSuggestedName_File_ReturnsName_WhenNameAvailable()
    {
        var (provider, _) = await CreateProviderAsync();

        var suggested = await provider.GetSuggestedFileName(
            provider.RootContainer.Identifier, "fresh.docx");

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

        Assert.False(await provider.CheckValidFileName(name));
    }

    [Fact]
    public async Task CheckValidName_AcceptsTypicalName()
    {
        var (provider, _) = await CreateProviderAsync();

        Assert.True(await provider.CheckValidFileName("my-file_2.docx"));
    }

    [Fact]
    public async Task GetAncestors_File_IncludesParentChain()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "outer"))!;
        var inner = (await provider.CreateWopiChildContainer(folder.Identifier, "inner"))!;
        var file = (await provider.CreateWopiChildFile(inner.Identifier, "deep.txt"))!;

        var ancestors = await provider.GetFileAncestors(file.Identifier);

        // Expected: [root, outer, inner]
        Assert.Equal(3, ancestors.Count);
        Assert.Equal(provider.RootContainer.Identifier, ancestors[0].Identifier);
        Assert.Equal("outer", ancestors[1].Name);
        Assert.Equal("inner", ancestors[2].Name);
    }
}
