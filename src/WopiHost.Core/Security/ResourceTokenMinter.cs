using System.Security.Claims;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Security;

/// <summary>
/// Default <see cref="IWopiResourceTokenMinter"/>. Resolves the user's permissions through
/// <see cref="IWopiPermissionProvider"/> and mints a fresh resource-scoped token via
/// <see cref="IWopiAccessTokenService"/>. Source-generated logging traces every mint at
/// <see cref="LogLevel.Debug"/> — see <c>ResourceTokenMinter.Logging.cs</c>.
/// </summary>
/// <remarks>
/// Registered as singleton in <c>AddWopi()</c> with <c>TryAddSingleton</c> so a host that wants
/// a custom token-minting policy (audit/telemetry decorator, external token-issuance service,
/// opaque revocable tokens) can register its own implementation either before or after the
/// <c>AddWopi()</c> call.
/// </remarks>
public sealed partial class ResourceTokenMinter(
    IWopiAccessTokenService accessTokenService,
    IWopiPermissionProvider permissionProvider,
    ILogger<ResourceTokenMinter> logger) : IWopiResourceTokenMinter
{
    /// <inheritdoc />
    public async Task<WopiAccessToken> MintForFileAsync(ClaimsPrincipal user, IWopiFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(file);

        var perms = await permissionProvider.GetFilePermissionsAsync(user, file, cancellationToken).ConfigureAwait(false);
        var token = await accessTokenService.IssueAsync(
            BuildRequest(user, file.Identifier, WopiResourceType.File, filePermissions: perms),
            cancellationToken).ConfigureAwait(false);
        LogFileTokenMinted(logger, user.GetUserId(), file.Identifier, perms);
        return token;
    }

    /// <inheritdoc />
    public async Task<WopiAccessToken> MintForContainerAsync(ClaimsPrincipal user, IWopiContainer container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(container);

        var perms = await permissionProvider.GetContainerPermissionsAsync(user, container, cancellationToken).ConfigureAwait(false);
        var token = await accessTokenService.IssueAsync(
            BuildRequest(user, container.Identifier, WopiResourceType.Container, containerPermissions: perms),
            cancellationToken).ConfigureAwait(false);
        LogContainerTokenMinted(logger, user.GetUserId(), container.Identifier, perms);
        return token;
    }

    /// <inheritdoc />
    public async Task<WopiAccessToken> MintMinimumPrivilegeAsync(ClaimsPrincipal user, string resourceId, WopiResourceType resourceType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(resourceId);

        var token = await accessTokenService.IssueAsync(
            BuildRequest(user, resourceId, resourceType),
            cancellationToken).ConfigureAwait(false);
        LogMinimumPrivilegeTokenMinted(logger, user.GetUserId(), resourceType, resourceId);
        return token;
    }

    /// <summary>
    /// Synchronous request-shape builder. No async state machine on this path means Infer# stays
    /// clean even when the await above it is on a same-class method (see #471 history).
    /// <see cref="WopiAccessTokenRequest"/> exposes both <see cref="WopiAccessTokenRequest.FilePermissions"/>
    /// and <see cref="WopiAccessTokenRequest.ContainerPermissions"/>; the token-issuing path
    /// consults the set whose <see cref="WopiAccessTokenRequest.ResourceType"/> matches and
    /// ignores the other.
    /// </summary>
    private static WopiAccessTokenRequest BuildRequest(
        ClaimsPrincipal user,
        string resourceId,
        WopiResourceType resourceType,
        WopiFilePermissions filePermissions = WopiFilePermissions.None,
        WopiContainerPermissions containerPermissions = WopiContainerPermissions.None) => new()
    {
        UserId = user.GetUserId(),
        UserDisplayName = user.FindFirstValue(ClaimTypes.Name),
        UserEmail = user.FindFirstValue(ClaimTypes.Email),
        ResourceId = resourceId,
        ResourceType = resourceType,
        FilePermissions = filePermissions,
        ContainerPermissions = containerPermissions,
    };
}
