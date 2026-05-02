using Azure.Storage.Blobs;
using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

[Collection(AzuriteCollection.Name)]
public class WopiBlobFileTests(AzuriteFixture azurite)
{
    private async Task<BlobContainerClient> CreateContainerAsync()
    {
        var serviceClient = azurite.CreateBlobServiceClient();
        var container = serviceClient.GetBlobContainerClient($"blobfile-{Guid.NewGuid():N}");
        await container.CreateIfNotExistsAsync();
        return container;
    }

    [Fact]
    public async Task CreateAsync_NonExistentBlob_ReturnsInstance_WithExistsFalse()
    {
        var container = await CreateContainerAsync();
        var blob = container.GetBlobClient("ghost.txt");

        var file = await WopiBlobFile.CreateAsync(blob, "ghost.txt", "fake-id", CancellationToken.None);

        Assert.False(file.Exists);
        Assert.Equal(0, file.Length);
        Assert.Equal(0, file.Size);
        Assert.Equal(DateTime.MinValue, file.LastWriteTimeUtc);
        Assert.Null(file.Version);
        Assert.Equal(string.Empty, file.Owner);
        Assert.Null(file.Checksum);
    }

    [Fact]
    public async Task ExistingBlob_NoMetadata_OwnerIsEmpty_ChecksumIsNull()
    {
        var container = await CreateContainerAsync();
        var blob = container.GetBlobClient("plain.txt");
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("body")))
        {
            await blob.UploadAsync(stream, overwrite: true);
        }

        var file = await WopiBlobFile.CreateAsync(blob, "plain.txt", "id", CancellationToken.None);

        Assert.True(file.Exists);
        Assert.Equal(string.Empty, file.Owner);
        Assert.Null(file.Checksum);
        Assert.Equal(4, file.Length);
        Assert.NotNull(file.Version);
    }

    [Fact]
    public async Task ExistingBlob_WithOwnerMetadata_OwnerIsReturned()
    {
        var container = await CreateContainerAsync();
        var blob = container.GetBlobClient("with-owner.txt");
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("body")))
        {
            await blob.UploadAsync(stream, overwrite: true);
        }
        await blob.SetMetadataAsync(new Dictionary<string, string> { [WopiBlobFile.OwnerMetadataKey] = "alice" });

        var file = await WopiBlobFile.CreateAsync(blob, "with-owner.txt", "id", CancellationToken.None);

        Assert.Equal("alice", file.Owner);
    }

    [Fact]
    public async Task ExistingBlob_WithSha256Metadata_ChecksumIsDecoded()
    {
        var container = await CreateContainerAsync();
        var blob = container.GetBlobClient("with-hash.bin");
        using (var stream = new MemoryStream(new byte[] { 0xAB, 0xCD }))
        {
            await blob.UploadAsync(stream, overwrite: true);
        }
        const string hex = "deadbeefcafebabe";
        await blob.SetMetadataAsync(new Dictionary<string, string> { [WopiBlobFile.Sha256MetadataKey] = hex });

        var file = await WopiBlobFile.CreateAsync(blob, "with-hash.bin", "id", CancellationToken.None);

        Assert.NotNull(file.Checksum);
        Assert.Equal(Convert.FromHexString(hex), file.Checksum);
    }

    [Fact]
    public async Task ExistingBlob_WithEmptySha256Metadata_ChecksumIsNull()
    {
        // Empty hash string should fall through the !string.IsNullOrEmpty check.
        var container = await CreateContainerAsync();
        var blob = container.GetBlobClient("empty-hash.bin");
        using (var stream = new MemoryStream(new byte[] { 0x01 }))
        {
            await blob.UploadAsync(stream, overwrite: true);
        }
        await blob.SetMetadataAsync(new Dictionary<string, string> { [WopiBlobFile.Sha256MetadataKey] = "" });

        var file = await WopiBlobFile.CreateAsync(blob, "empty-hash.bin", "id", CancellationToken.None);

        Assert.Null(file.Checksum);
    }

    [Fact]
    public async Task NameWithoutExtension_ReturnsWholeNameAsName_AndEmptyExtension()
    {
        var container = await CreateContainerAsync();
        var blob = container.GetBlobClient("readme");
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("x")))
        {
            await blob.UploadAsync(stream, overwrite: true);
        }

        var file = await WopiBlobFile.CreateAsync(blob, "readme", "id", CancellationToken.None);

        Assert.Equal("readme", file.Name);
        Assert.Equal(string.Empty, file.Extension);
    }

    [Fact]
    public async Task NestedPath_NameAndExtension_AreParsedFromLastSegment()
    {
        var container = await CreateContainerAsync();
        var blob = container.GetBlobClient("a/b/c/file.docx");
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("x")))
        {
            await blob.UploadAsync(stream, overwrite: true);
        }

        var file = await WopiBlobFile.CreateAsync(blob, "a/b/c/file.docx", "id", CancellationToken.None);

        Assert.Equal("file", file.Name);
        Assert.Equal("docx", file.Extension);
        Assert.Equal("a/b/c/file.docx", file.BlobPath);
    }

    [Fact]
    public async Task GetReadStream_ReturnsContent()
    {
        var container = await CreateContainerAsync();
        var blob = container.GetBlobClient("read.txt");
        const string body = "read-me";
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body)))
        {
            await blob.UploadAsync(stream, overwrite: true);
        }
        var file = await WopiBlobFile.CreateAsync(blob, "read.txt", "id", CancellationToken.None);

        await using var s = await file.GetReadStream();
        using var reader = new StreamReader(s);
        Assert.Equal(body, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task GetWriteStream_NoExistingMetadata_StillPersistsHash()
    {
        // GetWriteStream when properties.Metadata is empty exercises the fallback
        // `new Dictionary<string, string>(StringComparer.Ordinal)` branch.
        var container = await CreateContainerAsync();
        var blob = container.GetBlobClient("nometa.bin");
        using (var stream = new MemoryStream(Array.Empty<byte>()))
        {
            await blob.UploadAsync(stream, overwrite: true);
        }
        var file = await WopiBlobFile.CreateAsync(blob, "nometa.bin", "id", CancellationToken.None);

        await using (var s = await file.GetWriteStream())
        {
            await s.WriteAsync(new byte[] { 1, 2, 3 });
        }

        var props = await blob.GetPropertiesAsync();
        Assert.True(props.Value.Metadata.ContainsKey(WopiBlobFile.Sha256MetadataKey));
    }

    [Fact]
    public async Task GetWriteStream_OnNonExistentBlob_StillProducesUploadableStream()
    {
        // properties is null when the blob doesn't exist; the preserved-metadata branch falls back
        // to a fresh dictionary and OpenWriteAsync(overwrite:true) will create the blob.
        var container = await CreateContainerAsync();
        var blob = container.GetBlobClient("freshly-created.bin");
        var file = await WopiBlobFile.CreateAsync(blob, "freshly-created.bin", "id", CancellationToken.None);
        Assert.False(file.Exists);

        await using (var s = await file.GetWriteStream())
        {
            await s.WriteAsync(new byte[] { 9, 8, 7 });
        }

        Assert.True(await blob.ExistsAsync());
    }
}
