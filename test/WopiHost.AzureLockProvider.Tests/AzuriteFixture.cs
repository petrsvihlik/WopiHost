using Azure.Storage.Blobs;
using Testcontainers.Azurite;
using Xunit;

namespace WopiHost.AzureLockProvider.Tests;

public sealed class AzuriteFixture : IAsyncLifetime
{
    /// <summary>Latest stable Azurite at the time of writing.</summary>
    public const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.35.0";

    /// <summary>
    /// Latest blob service version Azurite 3.35.0 understands. Bump alongside <see cref="AzuriteImage"/>.
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
