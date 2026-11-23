using Azure.Storage.Blobs;
using Testcontainers.Azurite;
using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

/// <summary>
/// Provides an xUnit collection fixture that boots one Azurite container shared by every test
/// class in <see cref="AzuriteCollection"/>. Tests build clients via
/// <see cref="CreateBlobServiceClient"/>.
/// </summary>
/// <remarks>
/// The image tag is pinned to an Azurite that supports the API version the bundled
/// <c>Azure.Storage.Blobs</c> sends; <c>Testcontainers.Azurite</c>'s default image (3.28) lags
/// behind the SDK and is rejected by version-check.
/// </remarks>
public sealed class AzuriteFixture : IAsyncLifetime
{
    /// <summary>The Azurite container image the fixture runs.</summary>
    public const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.36.0";

    /// <summary>
    /// Latest blob service version the pinned Azurite image understands. The SDK ships a newer default
    /// (currently 2026-02-06) which Azurite rejects with <c>InvalidHeaderValue</c>, so tests pin one
    /// version older. Bump alongside <see cref="AzuriteImage"/>.
    /// </summary>
    public const BlobClientOptions.ServiceVersion AzuriteSupportedVersion = BlobClientOptions.ServiceVersion.V2025_11_05;

    private readonly AzuriteContainer _container = new AzuriteBuilder(AzuriteImage)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public BlobServiceClient CreateBlobServiceClient()
        => new(ConnectionString, new BlobClientOptions(AzuriteSupportedVersion));

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class AzuriteCollection : ICollectionFixture<AzuriteFixture>
{
    public const string Name = "Azurite collection";
}
