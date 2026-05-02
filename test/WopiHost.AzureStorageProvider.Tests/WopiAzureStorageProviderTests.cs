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
        _ = await provider.GetWopiResource<IWopiFolder>(provider.RootContainerPointer.Identifier);
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
        await foreach (var f in provider.GetWopiFiles(provider.RootContainerPointer.Identifier))
        {
            files.Add(f);
        }
        Assert.Single(files);

        var fetched = await provider.GetWopiResource<IWopiFile>(files[0].Identifier);
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
        var fetched = await provider.GetWopiResource<IWopiFile>("nonexistent");
        Assert.Null(fetched);
    }

    [Fact]
    public async Task GetReadStream_ReturnsBlobContent()
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "doc.txt", "stream-me");
        await foreach (var f in provider.GetWopiFiles(provider.RootContainerPointer.Identifier)) { _ = f; }
        var file = await provider.GetWopiResourceByName<IWopiFile>(provider.RootContainerPointer.Identifier, "doc.txt");

        Assert.NotNull(file);
        await using var s = await file.GetReadStream();
        using var reader = new StreamReader(s);
        Assert.Equal("stream-me", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task GetWriteStream_StoresContent_AndComputesSha256()
    {
        var (provider, _) = await CreateProviderAsync();
        var created = await provider.CreateWopiChildResource<IWopiFile>(null, "writeable.txt");
        Assert.NotNull(created);
        Assert.True(created.Exists);

        const string payload = "the quick brown fox";
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var writable = (await provider.GetWopiResource<IWopiFile>(created.Identifier))!;
        await using (var s = await writable.GetWriteStream())
        {
            await s.WriteAsync(System.Text.Encoding.UTF8.GetBytes(payload));
        }

        // Re-fetch so cached metadata reflects the new state.
        var refreshed = (await provider.GetWopiResource<IWopiFile>(created.Identifier))!;
        Assert.Equal(payload.Length, refreshed.Length);
        Assert.NotNull(refreshed.Checksum);
        Assert.Equal(expectedHash, Convert.ToHexString(refreshed.Checksum!).ToLowerInvariant());
    }

    [Fact]
    public async Task CreateWopiChildResource_File_AddsZeroByteBlob()
    {
        var (provider, _) = await CreateProviderAsync();

        var created = await provider.CreateWopiChildResource<IWopiFile>(null, "fresh.docx");

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
        _ = await provider.CreateWopiChildResource<IWopiFile>(null, "dup.txt");

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CreateWopiChildResource<IWopiFile>(null, "dup.txt"));
    }

    [Fact]
    public async Task CreateWopiChildResource_Folder_MaterializesMarkerBlob_AndIsListable()
    {
        var (provider, _) = await CreateProviderAsync();

        var folder = await provider.CreateWopiChildResource<IWopiFolder>(null, "subfolder");

        Assert.NotNull(folder);
        Assert.Equal("subfolder", folder.Name);

        var folders = new List<IWopiFolder>();
        await foreach (var f in provider.GetWopiContainers(provider.RootContainerPointer.Identifier))
        {
            folders.Add(f);
        }
        Assert.Contains(folders, f => f.Name == "subfolder");
    }

    [Fact]
    public async Task GetWopiFiles_HidesFolderMarker()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = await provider.CreateWopiChildResource<IWopiFolder>(null, "with-marker");

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
        var created = (await provider.CreateWopiChildResource<IWopiFile>(null, "doomed.txt"))!;

        var deleted = await provider.DeleteWopiResource<IWopiFile>(created.Identifier);

        Assert.True(deleted);
        Assert.Null(await provider.GetWopiResource<IWopiFile>(created.Identifier));
    }

    [Fact]
    public async Task DeleteWopiResource_Folder_NonEmpty_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildResource<IWopiFolder>(null, "with-content"))!;
        _ = await provider.CreateWopiChildResource<IWopiFile>(folder.Identifier, "child.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.DeleteWopiResource<IWopiFolder>(folder.Identifier));
    }

    [Fact]
    public async Task DeleteWopiResource_Folder_Empty_Succeeds()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildResource<IWopiFolder>(null, "ephemeral"))!;

        var deleted = await provider.DeleteWopiResource<IWopiFolder>(folder.Identifier);

        Assert.True(deleted);
    }

    [Fact]
    public async Task RenameWopiResource_File_PreservesIdentifier()
    {
        var (provider, _) = await CreateProviderAsync();
        var created = (await provider.CreateWopiChildResource<IWopiFile>(null, "before.txt"))!;
        var originalId = created.Identifier;

        var renamed = await provider.RenameWopiResource<IWopiFile>(originalId, "after.txt");

        Assert.True(renamed);
        var fetched = await provider.GetWopiResource<IWopiFile>(originalId);
        Assert.NotNull(fetched);
        Assert.Equal("after", fetched.Name);
    }

    [Fact]
    public async Task RenameWopiResource_File_TargetExists_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        var src = (await provider.CreateWopiChildResource<IWopiFile>(null, "source.txt"))!;
        _ = await provider.CreateWopiChildResource<IWopiFile>(null, "target.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.RenameWopiResource<IWopiFile>(src.Identifier, "target.txt"));
    }

    [Fact]
    public async Task GetSuggestedName_File_ReturnsCounterSuffix_WhenNameExists()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildResource<IWopiFile>(null, "report.docx");

        var suggested = await provider.GetSuggestedName<IWopiFile>(
            provider.RootContainerPointer.Identifier, "report.docx");

        Assert.Equal("report (1).docx", suggested);
    }

    [Fact]
    public async Task GetSuggestedName_File_ReturnsName_WhenNameAvailable()
    {
        var (provider, _) = await CreateProviderAsync();

        var suggested = await provider.GetSuggestedName<IWopiFile>(
            provider.RootContainerPointer.Identifier, "fresh.docx");

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

        Assert.False(await provider.CheckValidName<IWopiFile>(name));
    }

    [Fact]
    public async Task CheckValidName_AcceptsTypicalName()
    {
        var (provider, _) = await CreateProviderAsync();

        Assert.True(await provider.CheckValidName<IWopiFile>("my-file_2.docx"));
    }

    [Fact]
    public async Task GetAncestors_File_IncludesParentChain()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildResource<IWopiFolder>(null, "outer"))!;
        var inner = (await provider.CreateWopiChildResource<IWopiFolder>(folder.Identifier, "inner"))!;
        var file = (await provider.CreateWopiChildResource<IWopiFile>(inner.Identifier, "deep.txt"))!;

        var ancestors = await provider.GetAncestors<IWopiFile>(file.Identifier);

        // Expected: [root, outer, inner]
        Assert.Equal(3, ancestors.Count);
        Assert.Equal(provider.RootContainerPointer.Identifier, ancestors[0].Identifier);
        Assert.Equal("outer", ancestors[1].Name);
        Assert.Equal("inner", ancestors[2].Name);
    }
}
