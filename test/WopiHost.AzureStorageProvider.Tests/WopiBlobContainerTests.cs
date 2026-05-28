using Azure.Storage.Blobs;
using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

[Trait("Category", "Integration")]
[Collection(AzuriteCollection.Name)]
public class WopiBlobContainerTests(AzuriteFixture azurite)
{
    private async Task<BlobContainerClient> CreateContainerAsync()
    {
        var serviceClient = azurite.CreateBlobServiceClient();
        var container = serviceClient.GetBlobContainerClient($"blobcontainer-{Guid.NewGuid():N}");
        await container.CreateIfNotExistsAsync();
        return container;
    }

    private static async Task UploadAsync(BlobContainerClient container, string name, byte[] content)
    {
        await container.GetBlobClient(name).UploadAsync(new MemoryStream(content), overwrite: true);
    }

    [Fact]
    public async Task Size_EmptyContainer_ReturnsZero()
    {
        var container = await CreateContainerAsync();
        var sut = new WopiBlobContainer(prefix: string.Empty, identifier: "root-id", container);

        Assert.Equal(0L, sut.Size);
    }

    [Fact]
    public async Task Size_SumsAllDescendantBlobs_Recursive()
    {
        var container = await CreateContainerAsync();
        await UploadAsync(container, "folder/a.bin", new byte[10]);
        await UploadAsync(container, "folder/sub/b.bin", new byte[25]);
        // Sibling blob outside the queried prefix — must not be counted.
        await UploadAsync(container, "other/c.bin", new byte[7]);

        var sut = new WopiBlobContainer(prefix: "folder", identifier: "folder-id", container);

        Assert.Equal(35L, sut.Size);
    }

    [Fact]
    public async Task Size_IgnoresFolderMarkerBlobs()
    {
        var container = await CreateContainerAsync();
        await UploadAsync(container, $"folder/{BlobIdMap.FolderMarker}", new byte[100]); // marker — must not count
        await UploadAsync(container, "folder/real.bin", new byte[5]);

        var sut = new WopiBlobContainer(prefix: "folder", identifier: "folder-id", container);

        Assert.Equal(5L, sut.Size);
    }

    [Fact]
    public async Task Size_CachedAfterFirstAccess()
    {
        // Successive reads should return the same value without re-enumerating. We can't
        // observe the round-trip count directly, but if the value changes after a mutation
        // we know the property re-read. This pin is for the cache invariant: once read,
        // the value is stable for the lifetime of the WopiBlobContainer instance.
        var container = await CreateContainerAsync();
        await UploadAsync(container, "folder/a.bin", new byte[4]);

        var sut = new WopiBlobContainer(prefix: "folder", identifier: "folder-id", container);
        var first = sut.Size;
        // Mutate the underlying container after the cache is populated.
        await UploadAsync(container, "folder/b.bin", new byte[100]);
        var second = sut.Size;

        Assert.Equal(4L, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task ChildCount_CountsDirectChildrenOnly_NotRecursive()
    {
        var container = await CreateContainerAsync();
        await UploadAsync(container, "folder/a.txt", new byte[1]);
        await UploadAsync(container, "folder/b.txt", new byte[1]);
        // Sub-folder content — counts as ONE child (the sub-folder prefix), not three.
        await UploadAsync(container, "folder/sub/x.txt", new byte[1]);
        await UploadAsync(container, "folder/sub/y.txt", new byte[1]);
        await UploadAsync(container, "folder/sub/z.txt", new byte[1]);

        var sut = new WopiBlobContainer(prefix: "folder", identifier: "folder-id", container);

        Assert.Equal(3, sut.ChildCount); // a.txt + b.txt + sub/
    }

    [Fact]
    public async Task ChildCount_IgnoresFolderMarkerBlob()
    {
        var container = await CreateContainerAsync();
        await UploadAsync(container, $"folder/{BlobIdMap.FolderMarker}", []);
        await UploadAsync(container, "folder/real.txt", new byte[1]);

        var sut = new WopiBlobContainer(prefix: "folder", identifier: "folder-id", container);

        Assert.Equal(1, sut.ChildCount); // marker excluded, only real.txt counted
    }

    [Fact]
    public void Size_NullContainerClient_ReturnsZero()
    {
        // Offline-friendly mode used by tests that only need name/identifier semantics.
        var sut = new WopiBlobContainer(prefix: "folder", identifier: "folder-id", containerClient: null);
        Assert.Equal(0L, sut.Size);
        Assert.Equal(0, sut.ChildCount);
    }
}
