using WopiHost.Abstractions;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>Identity baked into a fixture-minted access token.</summary>
internal sealed record FixtureUser(string Id, string DisplayName, string Email);

/// <summary>
/// Mints WOPI access tokens against a fixture's booted backend. Shared by the read-only and
/// mutating endpoint fixtures so the token-issuing call lives in one place.
/// </summary>
internal static class FixtureTokens
{
    public static async Task<string> MintFileTokenAsync(
        WopiBackendFactory backend,
        FixtureUser user,
        string fileId,
        WopiFilePermissions permissions)
    {
        using var scope = backend.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IWopiAccessTokenService>();
        var token = await tokens.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = user.Id,
            UserDisplayName = user.DisplayName,
            UserEmail = user.Email,
            ResourceId = fileId,
            ResourceType = WopiResourceType.File,
            FilePermissions = permissions,
        });
        return token.Token;
    }

    public static async Task<string> MintContainerTokenAsync(
        WopiBackendFactory backend,
        FixtureUser user,
        string containerId)
    {
        using var scope = backend.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IWopiAccessTokenService>();
        var token = await tokens.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = user.Id,
            UserDisplayName = user.DisplayName,
            UserEmail = user.Email,
            ResourceId = containerId,
            ResourceType = WopiResourceType.Container,
            ContainerPermissions = WopiContainerPermissions.UserCanCreateChildContainer
                | WopiContainerPermissions.UserCanCreateChildFile
                | WopiContainerPermissions.UserCanDelete
                | WopiContainerPermissions.UserCanRename,
        });
        return token.Token;
    }
}
