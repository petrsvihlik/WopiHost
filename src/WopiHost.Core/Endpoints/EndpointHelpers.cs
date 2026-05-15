using System.Diagnostics.CodeAnalysis;
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
[ExcludeFromCodeCoverage(Justification = "Phase 2 of #430 migration; HTTP parity tests land in phase 5 (test relocation into WopiHost.IntegrationTests)")]
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
        var url = httpContext.GetUrlHelper().GetWopiSrc(WopiRouteNames.CheckEcosystem, identifier: null, accessToken: token.Token);
        return TypedResults.Json(new UrlResponse(url));
    }
}
