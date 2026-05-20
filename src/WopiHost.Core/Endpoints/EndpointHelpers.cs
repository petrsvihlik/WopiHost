using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Shared helpers for the WOPI Minimal-API endpoint handlers. Keep here only logic that is
/// genuinely cross-resource — single-resource concerns belong in their respective endpoint
/// file.
/// </summary>
internal static class EndpointHelpers
{
    /// <summary>
    /// Issues a minimum-privilege access token bound to <paramref name="resourceIdentifier"/>
    /// and returns a <see cref="UrlResponse"/> pointing at <see cref="WopiRouteNames.CheckEcosystem"/>.
    /// Used by the file- and container-side <c>ecosystem_pointer</c> endpoints. Reusing the
    /// inbound token would violate the WOPI "preventing token trading" guidance.
    /// </summary>
    public static async Task<IResult> IssueEcosystemPointerAsync(
        HttpContext httpContext,
        string resourceIdentifier,
        WopiResourceType resourceType,
        IWopiAccessTokenService accessTokenService,
        CancellationToken cancellationToken)
    {
        // WopiAccessTokenRequest exposes FilePermissions and ContainerPermissions as init-only
        // properties — set both to None unconditionally. The token-issuing path consults the
        // permission set whose ResourceType matches; the other is ignored.
        var request = new WopiAccessTokenRequest
        {
            UserId = httpContext.User.GetUserId(),
            UserDisplayName = httpContext.User.FindFirstValue(ClaimTypes.Name),
            UserEmail = httpContext.User.FindFirstValue(ClaimTypes.Email),
            ResourceId = resourceIdentifier,
            ResourceType = resourceType,
            FilePermissions = WopiFilePermissions.None,
            ContainerPermissions = WopiContainerPermissions.None,
        };

        var token = await accessTokenService.IssueAsync(request, cancellationToken).ConfigureAwait(false);
        var url = httpContext.GetWopiSrc(WopiRouteNames.CheckEcosystem, identifier: null, accessToken: token.Token);
        return TypedResults.Json(new UrlResponse(url));
    }

    /// <summary>
    /// Mints a fresh resource-scoped access token for <paramref name="file"/> and returns the
    /// token string. Used by <c>PutRelativeFile</c> and <c>CreateChildFile</c> to build the
    /// response <c>Url</c> property — reusing the inbound token (which is bound to the SOURCE
    /// file's resource id) would either fail downstream authorization or open a token-trading
    /// hole per
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/security#preventing-token-trading"/>.
    /// Permissions on the new file are resolved through <see cref="IWopiPermissionProvider"/>
    /// so a host that locks down create-vs-edit separately sees that distinction in the token's
    /// <c>wopi:fperms</c> claim.
    /// </summary>
    public static async Task<string> IssueAccessTokenForFileAsync(
        HttpContext httpContext,
        IWopiAccessTokenService accessTokenService,
        IWopiPermissionProvider permissionProvider,
        IWopiFile file,
        CancellationToken cancellationToken)
    {
        var perms = await permissionProvider.GetFilePermissionsAsync(httpContext.User, file, cancellationToken).ConfigureAwait(false);
        var request = new WopiAccessTokenRequest
        {
            UserId = httpContext.User.GetUserId(),
            UserDisplayName = httpContext.User.FindFirstValue(ClaimTypes.Name),
            UserEmail = httpContext.User.FindFirstValue(ClaimTypes.Email),
            ResourceId = file.Identifier,
            ResourceType = WopiResourceType.File,
            FilePermissions = perms,
        };
        var token = await accessTokenService.IssueAsync(request, cancellationToken).ConfigureAwait(false);
        return token.Token;
    }

    /// <summary>
    /// Container-shaped sibling of <see cref="IssueAccessTokenForFileAsync"/>. Mints a fresh
    /// access token bound to <paramref name="container"/>'s identifier and the user's container
    /// permissions, for use in responses that surface container URLs (EnumerateAncestors,
    /// CreateChildContainer, ...). Reusing the inbound file/container token across different
    /// resource ids violates the "preventing token trading" guidance.
    /// </summary>
    public static async Task<string> IssueAccessTokenForContainerAsync(
        HttpContext httpContext,
        IWopiAccessTokenService accessTokenService,
        IWopiPermissionProvider permissionProvider,
        IWopiContainer container,
        CancellationToken cancellationToken)
    {
        var perms = await permissionProvider.GetContainerPermissionsAsync(httpContext.User, container, cancellationToken).ConfigureAwait(false);
        var request = new WopiAccessTokenRequest
        {
            UserId = httpContext.User.GetUserId(),
            UserDisplayName = httpContext.User.FindFirstValue(ClaimTypes.Name),
            UserEmail = httpContext.User.FindFirstValue(ClaimTypes.Email),
            ResourceId = container.Identifier,
            ResourceType = WopiResourceType.Container,
            ContainerPermissions = perms,
        };
        var token = await accessTokenService.IssueAsync(request, cancellationToken).ConfigureAwait(false);
        return token.Token;
    }

    /// <summary>
    /// Parses the <c>X-WOPI-WopiSrc</c> header into a <see cref="WopiResourceType"/> and the
    /// resource identifier. Accepts paths shaped like <c>/wopi/files/{id}</c> or
    /// <c>/wopi/containers/{id}</c>. Shared between the Minimal-API bootstrap endpoint and
    /// (transitionally) the MVC bootstrap controller until phase 4 deletes the latter.
    /// </summary>
    public static bool TryParseWopiSrc(string wopiSrc, out WopiResourceType resourceType, out string resourceId)
    {
        resourceType = default;
        resourceId = string.Empty;

        if (string.IsNullOrWhiteSpace(wopiSrc) || !Uri.TryCreate(wopiSrc, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("files", StringComparison.OrdinalIgnoreCase))
            {
                resourceType = WopiResourceType.File;
                resourceId = Uri.UnescapeDataString(segments[i + 1]);
                return true;
            }
            if (segments[i].Equals("containers", StringComparison.OrdinalIgnoreCase))
            {
                resourceType = WopiResourceType.Container;
                resourceId = Uri.UnescapeDataString(segments[i + 1]);
                return true;
            }
        }
        return false;
    }
}
