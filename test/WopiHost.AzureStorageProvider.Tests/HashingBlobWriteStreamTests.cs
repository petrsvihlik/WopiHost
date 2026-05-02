using System.Security.Cryptography;
using System.Text;
using Azure.Storage.Blobs;
using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

[Collection(AzuriteCollection.Name)]
public class HashingBlobWriteStreamTests(AzuriteFixture azurite)
{
    private async Task<(BlobClient blobClient, BlobContainerClient container)> CreateBlobAsync()
    {
        var serviceClient = azurite.CreateBlobServiceClient();
        var container = serviceClient.GetBlobContainerClient($"hash-test-{Guid.NewGuid():N}");
        await container.CreateIfNotExistsAsync();
        var blob = container.GetBlobClient("subject.bin");
        // Pre-create the blob so OpenWriteAsync(overwrite:true) works.
        using var empty = new MemoryStream(Array.Empty<byte>(), writable: false);
        await blob.UploadAsync(empty, overwrite: false);
        return (blob, container);
    }

    private async Task<HashingBlobWriteStream> OpenWrapperAsync(BlobClient blobClient)
    {
        var inner = await blobClient.OpenWriteAsync(overwrite: true);
        return new HashingBlobWriteStream(inner, blobClient, new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private static string ExpectedSha256(string s)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    [Fact]
    public async Task Write_ByteArrayOffsetCount_PersistsContentAndHash()
    {
        var (blob, _) = await CreateBlobAsync();
        const string payload = "hello-byte-array";
        var bytes = Encoding.UTF8.GetBytes(payload);

        await using (var s = await OpenWrapperAsync(blob))
        {
            // Sync byte[]/offset/count overload
            s.Write(bytes, 0, bytes.Length);
        }

        var props = await blob.GetPropertiesAsync();
        Assert.Equal(payload.Length, props.Value.ContentLength);
        Assert.Equal(ExpectedSha256(payload), props.Value.Metadata[WopiBlobFile.Sha256MetadataKey]);
    }

    [Fact]
    public async Task Write_ReadOnlySpan_PersistsContentAndHash()
    {
        var (blob, _) = await CreateBlobAsync();
        const string payload = "span-payload";
        var bytes = Encoding.UTF8.GetBytes(payload);

        await using (var s = await OpenWrapperAsync(blob))
        {
            s.Write(bytes.AsSpan());
        }

        var props = await blob.GetPropertiesAsync();
        Assert.Equal(payload.Length, props.Value.ContentLength);
        Assert.Equal(ExpectedSha256(payload), props.Value.Metadata[WopiBlobFile.Sha256MetadataKey]);
    }

    [Fact]
    public async Task WriteAsync_ByteArrayOffsetCount_PersistsContentAndHash()
    {
        var (blob, _) = await CreateBlobAsync();
        const string payload = "async-byte-array";
        var bytes = Encoding.UTF8.GetBytes(payload);

        await using (var s = await OpenWrapperAsync(blob))
        {
            await s.WriteAsync(bytes, 0, bytes.Length, CancellationToken.None);
        }

        var props = await blob.GetPropertiesAsync();
        Assert.Equal(payload.Length, props.Value.ContentLength);
        Assert.Equal(ExpectedSha256(payload), props.Value.Metadata[WopiBlobFile.Sha256MetadataKey]);
    }

    [Fact]
    public async Task Flush_AndFlushAsync_DoNotThrow()
    {
        var (blob, _) = await CreateBlobAsync();

        await using var s = await OpenWrapperAsync(blob);
        s.Flush();
        await s.FlushAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CapabilityFlags_Match_WriteOnlyStream()
    {
        var (blob, _) = await CreateBlobAsync();
        await using var s = await OpenWrapperAsync(blob);

        Assert.False(s.CanRead);
        Assert.False(s.CanSeek);
        Assert.True(s.CanWrite);
    }

    [Fact]
    public async Task LengthAndPosition_Throw_NotSupported()
    {
        var (blob, _) = await CreateBlobAsync();
        await using var s = await OpenWrapperAsync(blob);

        Assert.Throws<NotSupportedException>(() => s.Length);
        Assert.Throws<NotSupportedException>(() => s.Position);
        Assert.Throws<NotSupportedException>(() => s.Position = 0);
    }

    [Fact]
    public async Task Read_Seek_SetLength_Throw_NotSupported()
    {
        var (blob, _) = await CreateBlobAsync();
        await using var s = await OpenWrapperAsync(blob);

        Assert.Throws<NotSupportedException>(() => s.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => s.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => s.SetLength(0));
    }

    [Fact]
    public async Task DisposeAsync_Idempotent()
    {
        var (blob, _) = await CreateBlobAsync();
        var s = await OpenWrapperAsync(blob);
        await s.WriteAsync(Encoding.UTF8.GetBytes("once"));

        await s.DisposeAsync();
        // Second dispose must be a no-op (does not re-call SetMetadata, doesn't throw).
        await s.DisposeAsync();
    }

    [Fact]
    public async Task SyncDispose_PersistsContentAndHash()
    {
        var (blob, _) = await CreateBlobAsync();
        const string payload = "sync-dispose-path";
        var bytes = Encoding.UTF8.GetBytes(payload);

        // Use plain `using` (sync dispose) instead of `await using` to exercise Dispose(true).
        using (var s = await OpenWrapperAsync(blob))
        {
            s.Write(bytes, 0, bytes.Length);
        }

        var props = await blob.GetPropertiesAsync();
        Assert.Equal(payload.Length, props.Value.ContentLength);
        Assert.Equal(ExpectedSha256(payload), props.Value.Metadata[WopiBlobFile.Sha256MetadataKey]);
    }

    [Fact]
    public async Task SyncDispose_Idempotent()
    {
        var (blob, _) = await CreateBlobAsync();
        var s = await OpenWrapperAsync(blob);
        s.Write([1, 2, 3], 0, 3);

        s.Dispose();
        s.Dispose(); // no-op on second call
    }

    [Fact]
    public async Task PreservedMetadata_RoundTrips_AlongsideHash()
    {
        // Caller-provided metadata (e.g. the previous wopi_owner) should survive the write.
        var (blob, _) = await CreateBlobAsync();
        const string payload = "preserve-metadata";
        var bytes = Encoding.UTF8.GetBytes(payload);

        var preserved = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["wopi_owner"] = "alice",
        };
        var inner = await blob.OpenWriteAsync(overwrite: true);
        await using (var s = new HashingBlobWriteStream(inner, blob, preserved))
        {
            await s.WriteAsync(bytes);
        }

        var props = await blob.GetPropertiesAsync();
        Assert.Equal("alice", props.Value.Metadata["wopi_owner"]);
        Assert.Equal(ExpectedSha256(payload), props.Value.Metadata[WopiBlobFile.Sha256MetadataKey]);
    }
}
