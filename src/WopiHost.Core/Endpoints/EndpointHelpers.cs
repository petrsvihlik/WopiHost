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
