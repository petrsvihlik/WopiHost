using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

/// <summary>
/// Targeted tests for branches the happy-path tests don't cover: error returns, unsupported types,
/// duplicate creation, root-folder operations, search-pattern matching, etc.
/// </summary>
[Trait("Category", "Integration")]
[Collection(AzuriteCollection.Name)]
public class WopiAzureStorageProviderEdgeCaseTests(AzuriteFixture azurite)
{
    // Filter arrays hoisted to static readonly to satisfy CA1861 (single allocation reused across
    // Theory iterations) and IDE0300 (collection-expression initializer).
    private static readonly string[] s_docxFilter = [".docx"];
    private static readonly string[] s_docxAndTxtFilter = [".docx", ".txt"];
    private static readonly string[] s_wildcardDocxFilter = ["*.docx"];

    private async Task<(WopiAzureStorageProvider provider, BlobContainerClient container)> CreateProviderAsync()
    {
        var serviceClient = azurite.CreateBlobServiceClient();
        var container = serviceClient.GetBlobContainerClient($"wopi-edge-{Guid.NewGuid():N}");
        var idMap = new BlobIdMap(NullLogger<BlobIdMap>.Instance);
        var provider = new WopiAzureStorageProvider(container, idMap, NullLogger<WopiAzureStorageProvider>.Instance);
        // Force init.
        _ = await provider.GetWopiContainer(provider.RootContainer.Identifier);
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
    public async Task GetWopiResource_Folder_RoundTripsViaIdentifier()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "folder1"))!;

        var fetched = await provider.GetWopiContainer(folder.Identifier);

        Assert.NotNull(fetched);
        Assert.Equal("folder1", fetched.Name);
    }

    [Fact]
    public async Task GetWopiResourceByName_UnknownContainer_ReturnsNull()
    {
        // #380 item 4.2 — missing parent returns null, consistent with WopiFileSystemProvider.
        // Was previously the only impl that returned null here; this pins the contract for both.
        var (provider, _) = await CreateProviderAsync();
        var result = await provider.GetWopiFileByName("does-not-exist", "anything.txt");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWopiResourceByName_Folder_FoundAndMissing()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "subA"))!;
        // The folder marker blob makes the folder discoverable.

        var found = await provider.GetWopiContainerByName(provider.RootContainer.Identifier, "subA");
        Assert.NotNull(found);
        Assert.Equal(folder.Identifier, found.Identifier);

        var missing = await provider.GetWopiContainerByName(provider.RootContainer.Identifier, "nope");
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetAncestors_UnknownIdentifier_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => provider.GetFileAncestors("not-real"));
    }

    [Fact]
    public async Task GetAncestors_Folder_TopLevel_ReturnsRootOnly()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "topfolder"))!;

        var ancestors = await provider.GetContainerAncestors(folder.Identifier);

        Assert.Single(ancestors);
        Assert.Equal(provider.RootContainer.Identifier, ancestors[0].Identifier);
    }

    [Fact]
    public async Task GetWopiFiles_SingleExtensionFilter_FiltersByExtension()
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "doc.docx");
        await UploadAsync(container, "sheet.xlsx");
        await UploadAsync(container, "notes.txt");

        var matched = new List<string>();
        await foreach (var f in provider.GetWopiFiles(provider.RootContainer.Identifier, s_docxFilter))
        {
            matched.Add(f.Name + "." + f.Extension);
        }

        Assert.Single(matched);
        Assert.Equal("doc.docx", matched[0]);
    }

    [Fact]
    public async Task GetWopiFiles_MultipleExtensionFilter_ReturnsUnion()
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "doc.docx");
        await UploadAsync(container, "sheet.xlsx");
        await UploadAsync(container, "notes.txt");

        var matched = new List<string>();
        await foreach (var f in provider.GetWopiFiles(provider.RootContainer.Identifier, s_docxAndTxtFilter))
        {
            matched.Add(f.Name + "." + f.Extension);
        }

        Assert.Equal(2, matched.Count);
        Assert.Contains("doc.docx", matched);
        Assert.Contains("notes.txt", matched);
    }

    [Fact]
    public async Task GetWopiFiles_ExtensionFilter_IsCaseInsensitive()
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "doc.DOCX");
        await UploadAsync(container, "notes.txt");

        var matched = new List<string>();
        await foreach (var f in provider.GetWopiFiles(provider.RootContainer.Identifier, s_docxFilter))
        {
            matched.Add(f.Name + "." + f.Extension);
        }

        Assert.Single(matched);
    }

    [Fact]
    public async Task GetWopiFiles_ExtensionFilter_WildcardCharactersMatchedLiterally()
    {
        // WOPI spec forbids wildcards in the filter list. The provider treats any glob-looking
        // character as a literal — passing "*.docx" matches only a file named literally "*.docx"
        // (which Azure won't let us upload anyway), so the result is empty. This pin guards
        // against anyone reintroducing a regex/glob translator on the assumption that the old
        // semantic is still in effect.
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "doc.docx");

        var matched = new List<string>();
        await foreach (var f in provider.GetWopiFiles(provider.RootContainer.Identifier, s_wildcardDocxFilter))
        {
            matched.Add(f.Name + "." + f.Extension);
        }

        Assert.Empty(matched);
    }

    [Fact]
    public async Task GetWopiFiles_NullOrEmptyExtensionFilter_ReturnsEverything()
    {
        var (provider, container) = await CreateProviderAsync();
        await UploadAsync(container, "a.txt");
        await UploadAsync(container, "b.docx");

        var nullCount = 0;
        await foreach (var _ in provider.GetWopiFiles(provider.RootContainer.Identifier, fileExtensions: null))
        {
            nullCount++;
        }
        var emptyCount = 0;
        await foreach (var _ in provider.GetWopiFiles(provider.RootContainer.Identifier, []))
        {
            emptyCount++;
        }

        Assert.Equal(2, nullCount);
        Assert.Equal(2, emptyCount);
    }

    [Fact]
    public async Task CreateWopiChildResource_Folder_DuplicateName_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "dup-folder");

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "dup-folder"));
    }

    [Fact]
    public async Task DeleteWopiResource_UnknownIdentifier_ReturnsFalse()
    {
        var (provider, _) = await CreateProviderAsync();
        var deleted = await provider.DeleteWopiFile("not-mapped");
        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteWopiResource_RootFolder_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.DeleteWopiContainer(provider.RootContainer.Identifier));
    }

    [Fact]
    public async Task RenameWopiResource_InvalidName_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        var file = (await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "rn-invalid.txt"))!;

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.RenameWopiFile(file.Identifier, "bad/name.txt"));
    }

    [Fact]
    public async Task RenameWopiResource_UnknownIdentifier_ReturnsFalse()
    {
        var (provider, _) = await CreateProviderAsync();
        var renamed = await provider.RenameWopiFile("not-mapped", "ok.txt");
        Assert.False(renamed);
    }

    [Fact]
    public async Task RenameWopiResource_RootFolder_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.RenameWopiContainer(provider.RootContainer.Identifier, "newroot"));
    }

    [Fact]
    public async Task RenameWopiResource_Folder_PreservesId_AndMovesChildren()
    {
        var (provider, _) = await CreateProviderAsync();
        var folder = (await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "rn-folder"))!;
        var originalId = folder.Identifier;
        var child = (await provider.CreateWopiChildFile(folder.Identifier, "child.txt"))!;

        var renamed = await provider.RenameWopiContainer(originalId, "renamed-folder");
        Assert.True(renamed);

        var refreshed = await provider.GetWopiContainer(originalId);
        Assert.NotNull(refreshed);
        Assert.Equal("renamed-folder", refreshed.Name);

        // The child file's identifier should still resolve, now under the new prefix.
        var movedChild = await provider.GetWopiFile(child.Identifier);
        Assert.NotNull(movedChild);
        Assert.Equal("child", movedChild.Name);
    }

    [Fact]
    public async Task GetSuggestedName_InvalidName_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.GetSuggestedFileName(provider.RootContainer.Identifier, "bad/name"));
    }

    [Fact]
    public async Task GetSuggestedName_UnknownContainer_Throws()
    {
        var (provider, _) = await CreateProviderAsync();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => provider.GetSuggestedFileName("not-real", "fresh.txt"));
    }

    [Fact]
    public async Task GetSuggestedName_Folder_AppendsCounter_WhenExists()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "dup");

        var suggested = await provider.GetSuggestedContainerName(
            provider.RootContainer.Identifier, "dup");

        Assert.Equal("dup (1)", suggested);
    }

    [Fact]
    public async Task GetSuggestedName_File_NoExtension_AppendsCounter()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildFile(provider.RootContainer.Identifier, "noext");

        var suggested = await provider.GetSuggestedFileName(
            provider.RootContainer.Identifier, "noext");

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

        Assert.False(await provider.CheckValidFileName("foobar"));
        Assert.False(await provider.CheckValidFileName(new string('x', 251)));
    }

    [Fact]
    public async Task GetWopiContainers_FromRoot_ListsTopLevelFoldersOnly()
    {
        var (provider, _) = await CreateProviderAsync();
        _ = await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "alpha");
        _ = await provider.CreateWopiChildContainer(provider.RootContainer.Identifier, "beta");
        // A nested folder shouldn't appear in the top-level listing.
        var alphaId = (await provider.GetWopiContainerByName(provider.RootContainer.Identifier, "alpha"))!.Identifier;
        _ = await provider.CreateWopiChildContainer(alphaId, "alpha-inner");

        var names = new List<string>();
        await foreach (var f in provider.GetWopiContainers(provider.RootContainer.Identifier))
        {
            names.Add(f.Name);
        }

        Assert.Equal(2, names.Count);
        Assert.Contains("alpha", names);
        Assert.Contains("beta", names);
    }

    [Fact]
    public async Task RootContainer_IsAddressableViaItsIdentifier()
    {
        var (provider, _) = await CreateProviderAsync();
        var root = await provider.GetWopiContainer(provider.RootContainer.Identifier);
        Assert.NotNull(root);
        Assert.Equal(string.Empty, root.Name);
    }
}
