using System.Net.Mime;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Bootstrap operation per
/// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/bootstrap.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the regular <c>/wopi/*</c> endpoints (which authenticate via the
/// <c>access_token</c> query parameter), the bootstrapper is called by Office mobile clients
/// with an OAuth2 Bearer token from the host's identity provider. Configure that scheme as
/// <see cref="WopiAuthenticationSchemes.Bootstrap"/> and supply the IdP discovery URLs via the
/// <c>WWW-Authenticate</c> challenge.
/// </para>
/// </remarks>
[Authorize(AuthenticationSchemes = WopiAuthenticationSchemes.Bootstrap)]
[ApiController]
[Route("wopibootstrapper")]
[ServiceFilter(typeof(WopiOriginValidationActionFilter))]
public class WopiBootstrapperController(
    IWopiStorageProvider storageProvider,
    IWopiAccessTokenService accessTokenService,
    IWopiPermissionProvider permissionProvider) : ControllerBase
{
    /// <summary>
    /// Bootstrap entry point.
    /// </summary>
    [HttpPost]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> GetRootContainer(
        [FromHeader(Name = WopiHeaders.ECOSYSTEM_OPERATION)] string? ecosystemOperation = null,
        [FromHeader(Name = WopiHeaders.WOPI_SRC)] string? wopiSrc = null,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Upn)
            ?? throw new InvalidOperationException("Bootstrap principal lacks an identifier claim.");

        var bootstrapRoot = new BootstrapRootContainerInfo
        {
            Bootstrap = new BootstrapInfo
            {
                EcosystemUrl = Url.GetWopiSrc(WopiRouteNames.CheckEcosystem),
                SignInName = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                UserFriendlyName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                UserId = userId,
            }
        };

        if (ecosystemOperation == "GET_ROOT_CONTAINER")
        {
            var rootContainer = storageProvider.RootContainerPointer;
            var token = await IssueContainerTokenAsync(userId, rootContainer, cancellationToken);
            bootstrapRoot.RootContainerInfo = new RootContainerInfo
            {
                ContainerPointer = new ChildContainer(
                    rootContainer.Name,
                    Url.GetWopiSrc(WopiResourceType.Container, rootContainer.Identifier, token.Token))
            };
        }
        else if (ecosystemOperation == "GET_NEW_ACCESS_TOKEN")
        {
            ArgumentException.ThrowIfNullOrEmpty(wopiSrc);
            var resourceId = GetIdFromUrl(wopiSrc);
            var file = await storageProvider.GetWopiResource<IWopiFile>(resourceId, cancellationToken);
            var token = await IssueFileTokenAsync(userId, resourceId, file, cancellationToken);
            bootstrapRoot.AccessTokenInfo = new AccessTokenInfo
            {
                AccessToken = token.Token,
                AccessTokenExpiry = token.ExpiresAt.ToUnixTimeSeconds(),
            };
        }
        else
        {
            return new NotImplementedResult();
        }
        return new JsonResult(bootstrapRoot);
    }

    private async Task<WopiAccessToken> IssueFileTokenAsync(string userId, string resourceId, IWopiFile? file, CancellationToken cancellationToken)
    {
        var perms = file is null ? WopiFilePermissions.None : await permissionProvider.GetFilePermissionsAsync(User, file, cancellationToken);
        return await accessTokenService.IssueAsync(BuildRequest(userId, resourceId, WopiResourceType.File) with { FilePermissions = perms }, cancellationToken);
    }

    private async Task<WopiAccessToken> IssueContainerTokenAsync(string userId, IWopiFolder container, CancellationToken cancellationToken)
    {
        var perms = await permissionProvider.GetContainerPermissionsAsync(User, container, cancellationToken);
        return await accessTokenService.IssueAsync(BuildRequest(userId, container.Identifier, WopiResourceType.Container) with { ContainerPermissions = perms }, cancellationToken);
    }

    private WopiAccessTokenRequest BuildRequest(string userId, string resourceId, WopiResourceType resourceType) => new()
    {
        UserId = userId,
        UserDisplayName = User.FindFirstValue(ClaimTypes.Name),
        UserEmail = User.FindFirstValue(ClaimTypes.Email),
        ResourceId = resourceId,
        ResourceType = resourceType,
    };

    private static string GetIdFromUrl(string resourceUrl)
    {
        var resourceId = resourceUrl[(resourceUrl.LastIndexOf('/') + 1)..];
        var queryIndex = resourceId.IndexOf('?');
        if (queryIndex > -1)
        {
            resourceId = resourceId[..queryIndex];
        }
        return Uri.UnescapeDataString(resourceId);
    }
}
