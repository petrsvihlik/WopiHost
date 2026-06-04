using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions.Testing;
using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

/// <summary>
/// Runs the shared <see cref="StorageProviderConformanceTests"/> against <see cref="WopiAzureStorageProvider"/>
/// backed by an Azurite container.
/// </summary>
[Trait("Category", "Integration")]
[Collection(AzuriteCollection.Name)]
public sealed class AzureStorageProviderConformanceTests(AzuriteFixture azurite) : StorageProviderConformanceTests
{
    /// <inheritdoc />
    protected override IStorageProviderTestFactory Factory { get; } = new AzureFactory(azurite);

    private sealed class AzureFactory(AzuriteFixture azurite) : IStorageProviderTestFactory
    {
        public async Task<IStorageProviderTestContext> CreateAsync()
        {
            // Unique blob container per call so providers don't share state.
            var serviceClient = azurite.CreateBlobServiceClient();
            var container = serviceClient.GetBlobContainerClient($"wopi-conf-{Guid.NewGuid():N}");
            var provider = new WopiAzureStorageProvider(
                container,
                new BlobIdMap(NullLogger<BlobIdMap>.Instance),
                NullLogger<WopiAzureStorageProvider>.Instance);

            // First resolve creates the blob container and initializes the root.
            _ = await provider.GetWopiContainer(provider.RootContainer.Identifier);

            return new StorageProviderTestContext(provider, provider,
                async () => await container.DeleteIfExistsAsync());
        }
    }
}
