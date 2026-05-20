using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// Shared fixture for the read-only endpoint test classes (File / Container / Folder /
/// Ecosystem). Boots the WOPI backend once against the shared sample/wopi-docs root and
/// exposes ready-to-use resource identifiers + token-minting helpers so each test class
/// stays focused on assertions rather than setup.
/// </summary>
/// <remarks>
/// Read-only tests can safely share a single backend because they don't mutate the
/// file-system state. <see cref="FileMutatingEndpointTests"/> /
/// <see cref="ContainerMutatingEndpointTests"/> share their own collection-scoped temp-dir
/// fixture (so writes don't corrupt this shared one).
/// </remarks>
public sealed class ReadOnlyEndpointsFixture : IDisposable
{
    private const string SharedSigningSecret = "readonly-endpoints-shared-key-32bytes!";

    public WopiBackendFactory WopiBackend { get; }
    public string FirstFileId { get; }
    public string RootContainerId { get; }

    public ReadOnlyEndpointsFixture()
    {
        WopiBackend = new WopiBackendFactory(SharedSigningSecret);

        using var scope = WopiBackend.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>();
        RootContainerId = storage.RootContainer.Identifier;
        FirstFileId = ResolveFirstFileId(storage).GetAwaiter().GetResult();
    }

    public async Task<string> MintFileTokenAsync(string fileId, WopiFilePermissions permissions = WopiFilePermissions.UserCanWrite)
    {
        using var scope = WopiBackend.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IWopiAccessTokenService>();
        var token = await tokens.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "readonly-user",
            UserDisplayName = "ReadOnly User",
            UserEmail = "readonly@example.com",
            ResourceId = fileId,
            ResourceType = WopiResourceType.File,
            FilePermissions = permissions,
        });
        return token.Token;
    }

    public async Task<string> MintContainerTokenAsync(string containerId)
    {
        using var scope = WopiBackend.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IWopiAccessTokenService>();
        var token = await tokens.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "readonly-user",
            UserDisplayName = "ReadOnly User",
            UserEmail = "readonly@example.com",
            ResourceId = containerId,
            ResourceType = WopiResourceType.Container,
            ContainerPermissions = WopiContainerPermissions.UserCanCreateChildContainer
                | WopiContainerPermissions.UserCanCreateChildFile
                | WopiContainerPermissions.UserCanDelete
                | WopiContainerPermissions.UserCanRename,
        });
        return token.Token;
    }

    private static async Task<string> ResolveFirstFileId(IWopiStorageProvider storage)
    {
        await foreach (var f in storage.GetWopiFiles(storage.RootContainer.Identifier))
        {
            return f.Identifier;
        }
        throw new InvalidOperationException("sample/wopi-docs is empty — at least one file is required for the read-only fixture.");
    }

    public void Dispose() => WopiBackend.Dispose();
}

/// <summary>
/// xUnit collection wrapper so File / Container / Folder / Ecosystem endpoint test classes
/// can share a single <see cref="ReadOnlyEndpointsFixture"/> instance. Without the
/// collection, each class would boot its own backend (acceptable but ~3-5s of redundant
/// startup overhead per class).
/// </summary>
[CollectionDefinition("ReadOnlyEndpoints")]
public sealed class ReadOnlyEndpointsCollection : ICollectionFixture<ReadOnlyEndpointsFixture>
{
}
