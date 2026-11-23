using System.Security.Claims;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Security;

/// <summary>
/// Provides the default <see cref="IWopiResourceTokenMinter"/>, which resolves the user's permissions
/// through <see cref="IWopiPermissionProvider"/> and mints a fresh resource-scoped token via
/// <see cref="IWopiAccessTokenService"/>.
/// </summary>
/// <param name="accessTokenService">The service that issues the tokens.</param>
/// <param name="permissionProvider">The provider that resolves the user's permissions on the resource.</param>
/// <param name="logger">The logger; every mint is traced at <see cref="LogLevel.Debug"/>.</param>
/// <remarks>
/// <c>AddWopi()</c> registers it with <c>TryAddSingleton</c>, so a host that wants a custom
/// token-minting policy (audit/telemetry decorator, external token-issuance service, opaque revocable
/// tokens) can register its own implementation either before or after the <c>AddWopi()</c> call.
/// </remarks>
public sealed partial class ResourceTokenMinter(
    IWopiAccessTokenService accessTokenService,
    IWopiPermissionProvider permissionProvider,
    ILogger<ResourceTokenMinter> logger) : IWopiResourceTokenMinter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="user"/> or <paramref name="file"/> is <see langword="null"/>.</exception>
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
    /// <exception cref="ArgumentNullException"><paramref name="user"/> or <paramref name="container"/> is <see langword="null"/>.</exception>
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
    /// <exception cref="ArgumentNullException"><paramref name="user"/> or <paramref name="resourceId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="resourceId"/> is empty.</exception>
    public async Task<WopiAccessToken> MintForEcosystemAsync(ClaimsPrincipal user, string resourceId, WopiResourceType resourceType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(resourceId);

        var token = await accessTokenService.IssueAsync(
            BuildRequest(user, resourceId, resourceType),
            cancellationToken).ConfigureAwait(false);
        LogEcosystemTokenMinted(logger, user.GetUserId(), resourceType, resourceId);
        return token;
    }

    /// <summary>
    /// Builds the token request for the user and resource.
    /// </summary>
    /// <returns>The token request.</returns>
    /// <remarks>
    /// <para>
    /// Kept synchronous: with no async state machine on this path, static analysis stays clean even
    /// though the awaits above it are in the same class.
    /// </para>
    /// <para>
    /// Only the permission set matching <paramref name="resourceType"/> is supplied; the other is left
    /// at <c>None</c>.
    /// </para>
    /// </remarks>
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
