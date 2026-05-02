using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

/// <summary>
/// Targeted tests for branches the happy-path tests don't cover: error returns, unsupported types,
/// duplicate creation, root-folder operations, search-pattern matching, etc.
/// </summary>
[Collection(AzuriteCollection.Name)]
public class WopiAzureStorageProviderEdgeCaseTests(AzuriteFixture azurite)
{
    private async Task<(WopiAzureStorageProvider provider, BlobContainerClient container)> CreateProviderAsync()
    {
        var serviceClient = azurite.CreateBlobServiceClient();
        var container = serviceClient.GetBlobContainerClient($"wopi-edge-{Guid.NewGuid():N}");
        var idMap = new BlobIdMap(NullLogger<BlobIdMap>.Instance);
        var provider = new WopiAzureStorageProvider(container, idMap, NullLogger<WopiAzureStorageProvider>.Instance);
        // Force init.
        _ = await provider.GetWopiResource<IWopiFolder>(provider.RootContainerPointer.Identifier);
        return (provider, container);
    }

    private static async Task UploadAsync(BlobContainerClient container, string path, string content = "")
    {
        var blob = container.GetBlobClient(path);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await blob.UploadAsync(stream, overwrite: true);
    }

    [Fact]
    public void Constructor_NullArgs_Throw()
    {
        var serviceClient = azurite.CreateBlobServiceClient();
        var container = serviceClient.GetBlobContainerClient("ctor-test");
        var idMap = new BlobIdMap(NullLogger<BlobIdMap>.Instance);

        Assert.Throws<ArgumentNullException>(
            () => new WopiAzureStorageProvider(null!, idMap, NullLogger<WopiAzureStorageProvider>.Instance));
        Assert.Throws<ArgumentNullException>(
            () => new WopiAzureStorageProvider(container, null!, NullLogger<WopiAzureStorageProvider>.Instance));
        Assert.Throws<ArgumentNullException>(
            () => new WopiAzureStorageProvider(container, idMap, null!));
    }

    [Fact]
    public async Task GetWopiResource_UnsupportedType_Throws()
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "x.txt");
        // Drain GetWopiFiles to populate the id map for the blob.
        await foreach (var _ in provider.GetWopiFiles(provider.RootContainerPointer.Identifier)) { }
        var anyId = BlobIdMap.IdFromPath("x.txt");

        await Assert.ThrowsAsync<NotSupportedException>(
            () => provider.GetWopiResource<UnsupportedResource>(anyId));
    }

    [Fact]
    public async Task GetWopiResource_Folder_RoundTripsViaIdentifier()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildResource<IWopiFolder>(null, "folder1"))!;

        var fetched = await provider.GetWopiResource<IWopiFolder>(folder.Identifier);

        Assert.NotNull(fetched);
        Assert.Equal("folder1", fetched.Name);
    }

    [Fact]
    public async Task GetWopiResourceByName_UnknownContainer_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => provider.GetWopiResourceByName<IWopiFile>("does-not-exist", "anything.txt"));
    }

    [Fact]
    public async Task GetWopiResourceByName_Folder_FoundAndMissing()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildResource<IWopiFolder>(null, "subA"))!;
        // The folder marker blob makes the folder discoverable.

        var found = await provider.GetWopiResourceByName<IWopiFolder>(provider.RootContainerPointer.Identifier, "subA");
        Assert.NotNull(found);
        Assert.Equal(folder.Identifier, found.Identifier);

        var missing = await provider.GetWopiResourceByName<IWopiFolder>(provider.RootContainerPointer.Identifier, "nope");
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetWopiResourceByName_UnsupportedType_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => provider.GetWopiResourceByName<UnsupportedResource>(provider.RootContainerPointer.Identifier, "x"));
    }

    [Fact]
    public async Task GetAncestors_UnknownIdentifier_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => provider.GetAncestors<IWopiFile>("not-real"));
    }

    [Fact]
    public async Task GetAncestors_Folder_TopLevel_ReturnsRootOnly()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildResource<IWopiFolder>(null, "topfolder"))!;

        var ancestors = await provider.GetAncestors<IWopiFolder>(folder.Identifier);

        Assert.Single(ancestors);
        Assert.Equal(provider.RootContainerPointer.Identifier, ancestors[0].Identifier);
    }

    [Fact]
    public async Task GetWopiFiles_SearchPattern_FiltersByExtension()
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "doc.docx");
        await UploadAsync(container, "sheet.xlsx");
        await UploadAsync(container, "notes.txt");

        var matched = new List<string>();
        await foreach (var f in provider.GetWopiFiles(provider.RootContainerPointer.Identifier, searchPattern: "*.docx"))
        {
            matched.Add(f.Name + "." + f.Extension);
        }

        Assert.Single(matched);
        Assert.Equal("doc.docx", matched[0]);
    }

    [Fact]
    public async Task GetWopiFiles_SearchPattern_QuestionMarkWildcard()
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "a.txt");
        await UploadAsync(container, "ab.txt");

        var matched = new List<string>();
        // "?.txt" → exactly one char before .txt
        await foreach (var f in provider.GetWopiFiles(provider.RootContainerPointer.Identifier, searchPattern: "?.txt"))
        {
            matched.Add(f.Name + "." + f.Extension);
        }

        Assert.Single(matched);
        Assert.Equal("a.txt", matched[0]);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("*.*")]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetWopiFiles_NoPatternOrWildcard_ReturnsEverything(string? pattern)
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "a.txt");
        await UploadAsync(container, "b.docx");

        var count = 0;
        await foreach (var _ in provider.GetWopiFiles(provider.RootContainerPointer.Identifier, pattern))
        {
            count++;
        }

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CreateWopiChildResource_Folder_DuplicateName_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildResource<IWopiFolder>(null, "dup-folder");

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CreateWopiChildResource<IWopiFolder>(null, "dup-folder"));
    }

    [Fact]
    public async Task CreateWopiChildResource_UnsupportedType_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => provider.CreateWopiChildResource<UnsupportedResource>(null, "x"));
    }

    [Fact]
    public async Task DeleteWopiResource_UnknownIdentifier_ReturnsFalse()
    {
        var (provider, _) = await CreateProviderAsync();
        var deleted = await provider.DeleteWopiResource<IWopiFile>("not-mapped");
        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteWopiResource_RootFolder_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.DeleteWopiResource<IWopiFolder>(provider.RootContainerPointer.Identifier));
    }

    [Fact]
    public async Task DeleteWopiResource_UnsupportedType_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildResource<IWopiFolder>(null, "for-delete-unsupported"))!;
        await Assert.ThrowsAsync<NotSupportedException>(
            () => provider.DeleteWopiResource<UnsupportedResource>(folder.Identifier));
    }

    [Fact]
    public async Task RenameWopiResource_InvalidName_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        var file = (await provider.CreateWopiChildResource<IWopiFile>(null, "rn-invalid.txt"))!;

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.RenameWopiResource<IWopiFile>(file.Identifier, "bad/name.txt"));
    }

    [Fact]
    public async Task RenameWopiResource_UnknownIdentifier_ReturnsFalse()
    {
        var (provider, _) = await CreateProviderAsync();
        var renamed = await provider.RenameWopiResource<IWopiFile>("not-mapped", "ok.txt");
        Assert.False(renamed);
    }

    [Fact]
    public async Task RenameWopiResource_RootFolder_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.RenameWopiResource<IWopiFolder>(provider.RootContainerPointer.Identifier, "newroot"));
    }

    [Fact]
    public async Task RenameWopiResource_UnsupportedType_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        var file = (await provider.CreateWopiChildResource<IWopiFile>(null, "rn-unsupported.txt"))!;
        await Assert.ThrowsAsync<NotSupportedException>(
            () => provider.RenameWopiResource<UnsupportedResource>(file.Identifier, "x"));
    }

    [Fact]
    public async Task RenameWopiResource_Folder_PreservesId_AndMovesChildren()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildResource<IWopiFolder>(null, "rn-folder"))!;
        var originalId = folder.Identifier;
        var child = (await provider.CreateWopiChildResource<IWopiFile>(folder.Identifier, "child.txt"))!;

        var renamed = await provider.RenameWopiResource<IWopiFolder>(originalId, "renamed-folder");
        Assert.True(renamed);

        var refreshed = await provider.GetWopiResource<IWopiFolder>(originalId);
        Assert.NotNull(refreshed);
        Assert.Equal("renamed-folder", refreshed.Name);

        // The child file's identifier should still resolve, now under the new prefix.
        var movedChild = await provider.GetWopiResource<IWopiFile>(child.Identifier);
        Assert.NotNull(movedChild);
        Assert.Equal("child", movedChild.Name);
    }

    [Fact]
    public async Task GetSuggestedName_InvalidName_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.GetSuggestedName<IWopiFile>(provider.RootContainerPointer.Identifier, "bad/name"));
    }

    [Fact]
    public async Task GetSuggestedName_UnknownContainer_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => provider.GetSuggestedName<IWopiFile>("not-real", "fresh.txt"));
    }

    [Fact]
    public async Task GetSuggestedName_Folder_AppendsCounter_WhenExists()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildResource<IWopiFolder>(null, "dup");

        var suggested = await provider.GetSuggestedName<IWopiFolder>(
            provider.RootContainerPointer.Identifier, "dup");

        Assert.Equal("dup (1)", suggested);
    }

    [Fact]
    public async Task GetSuggestedName_File_NoExtension_AppendsCounter()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildResource<IWopiFile>(null, "noext");

        var suggested = await provider.GetSuggestedName<IWopiFile>(
            provider.RootContainerPointer.Identifier, "noext");

        Assert.Equal("noext (1)", suggested);
    }

    [Fact]
    public async Task FileNameMaxLength_Default_Is250()
    {
        var (provider, _) = await CreateProviderAsync();
        Assert.Equal(250, provider.FileNameMaxLength);
    }

    [Fact]
    public async Task CheckValidName_RejectsControlCharsAndOverlongNames()
    {
        var (provider, _) = await CreateProviderAsync();

        Assert.False(await provider.CheckValidName<IWopiFile>("foobar"));
        Assert.False(await provider.CheckValidName<IWopiFile>(new string('x', 251)));
    }

    [Fact]
    public async Task GetWopiContainers_FromRoot_ListsTopLevelFoldersOnly()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildResource<IWopiFolder>(null, "alpha");
        _ = await provider.CreateWopiChildResource<IWopiFolder>(null, "beta");
        // A nested folder shouldn't appear in the top-level listing.
        var alphaId = (await provider.GetWopiResourceByName<IWopiFolder>(provider.RootContainerPointer.Identifier, "alpha"))!.Identifier;
        _ = await provider.CreateWopiChildResource<IWopiFolder>(alphaId, "alpha-inner");

        var names = new List<string>();
        await foreach (var f in provider.GetWopiContainers(provider.RootContainerPointer.Identifier))
        {
            names.Add(f.Name);
        }

        Assert.Equal(2, names.Count);
        Assert.Contains("alpha", names);
        Assert.Contains("beta", names);
    }

    [Fact]
    public async Task RootContainerPointer_IsAddressableViaItsIdentifier()
    {
        var (provider, _) = await CreateProviderAsync();
        var root = await provider.GetWopiResource<IWopiFolder>(provider.RootContainerPointer.Identifier);
        Assert.NotNull(root);
        Assert.Equal(string.Empty, root.Name);
    }

    /// <summary>An <see cref="IWopiResource"/> that is neither a file nor a folder.</summary>
    public sealed class UnsupportedResource : IWopiResource
    {
        public string Name => string.Empty;
        public string Identifier => string.Empty;
    }
}
