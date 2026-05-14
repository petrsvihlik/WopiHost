using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using WopiHost.Abstractions.Testing;
using Xunit;

namespace WopiHost.AzureLockProvider.Tests;

/// <summary>
/// Runs the shared <see cref="LockProviderConformanceTests"/> against <see cref="WopiAzureLockProvider"/>
/// using an Azurite container per test.
/// </summary>
/// <remarks>
/// The <see cref="AzuriteCollection"/> attribute is required so the heavy Azurite container is
/// shared across all conformance and impl-specific tests in this project. Each test still gets
/// its own blob container (GUID-suffixed) for isolation. Provider-specific tests (blob-lease
/// takeover scenarios, direct metadata seeding) stay in <c>WopiAzureLockProviderTests</c>.
/// </remarks>
[Collection(AzuriteCollection.Name)]
public sealed class AzureLockProviderConformanceTests(AzuriteFixture azurite) : LockProviderConformanceTests
{
    /// <inheritdoc />
    protected override ILockProviderTestFactory Factory { get; } = new AzureFactory(azurite);

    private sealed class AzureFactory(AzuriteFixture azurite) : ILockProviderTestFactory
    {
        public async Task<IWopiLockProvider> CreateAsync(TimeProvider timeProvider, IWopiLockComparer? lockComparer = null)
        {
            var serviceClient = azurite.CreateBlobServiceClient();
            var container = serviceClient.GetBlobContainerClient($"wopi-locks-conf-{Guid.NewGuid():N}");
            await container.CreateIfNotExistsAsync();
            return new WopiAzureLockProvider(container, NullLogger<WopiAzureLockProvider>.Instance, timeProvider, lockComparer);
        }
    }
}
