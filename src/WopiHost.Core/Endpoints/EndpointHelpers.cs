using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
internal static partial class EndpointHelpers
{
    /// <summary>
    /// Matches a trailing <c>/files/{id}</c> or <c>/containers/{id}</c> pair anywhere in a
    /// path. Anchored at the end so that a host serving WOPI under a parent path that happens
    /// to contain a segment named <c>files</c> or <c>containers</c> (e.g.
    /// <c>/repository/files/wopi/containers/abc</c>) still resolves to the WOPI resource at the
    /// tail. The <c>[^/?#]+</c> character class is defensive — <see cref="Uri.AbsolutePath"/>
    /// never includes query / fragment, but rejecting those characters costs nothing.
    /// <see cref="RegexOptions.CultureInvariant"/> pairs with <see cref="RegexOptions.IgnoreCase"/>
    /// so the literal "files" / "containers" match is locale-independent (Turkish dotless-I
    /// would otherwise surprise us).
    /// </summary>
    [GeneratedRegex(@"/(files|containers)/([^/?#]+)/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WopiSrcPathRegex();

    /// <summary>
    /// Issues a minimum-privilege access token bound to <paramref name="resourceIdentifier"/>
    /// and returns a <see cref="UrlResponse"/> pointing at <see cref="WopiRouteNames.CheckEcosystem"/>.
    /// Used by the file- and container-side <c>ecosystem_pointer</c> endpoints. Reusing the
    /// inbound token would violate the WOPI "preventing token trading" guidance.
    /// </summary>
    public static async Task<JsonHttpResult<UrlResponse>> IssueEcosystemPointerAsync(
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
        return await IssueResourceTokenAsync(httpContext, accessTokenService, file.Identifier, WopiResourceType.File,
            filePermissions: perms, containerPermissions: WopiContainerPermissions.None, cancellationToken).ConfigureAwait(false);
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
        return await IssueResourceTokenAsync(httpContext, accessTokenService, container.Identifier, WopiResourceType.Container,
            filePermissions: WopiFilePermissions.None, containerPermissions: perms, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Shared back-end for the typed <see cref="IssueAccessTokenForFileAsync"/> /
    /// <see cref="IssueAccessTokenForContainerAsync"/> helpers. WopiAccessTokenRequest carries
    /// both FilePermissions and ContainerPermissions as init-only properties; the token-issuing
    /// path consults the set whose ResourceType matches the request — passing
    /// <c>None</c> for the other is safe and idiomatic (same pattern as
    /// <see cref="IssueEcosystemPointerAsync"/>).
    /// </summary>
    private static async Task<string> IssueResourceTokenAsync(
        HttpContext httpContext,
        IWopiAccessTokenService accessTokenService,
        string resourceId,
        WopiResourceType resourceType,
        WopiFilePermissions filePermissions,
        WopiContainerPermissions containerPermissions,
        CancellationToken cancellationToken)
    {
        var request = new WopiAccessTokenRequest
        {
            UserId = httpContext.User.GetUserId(),
            UserDisplayName = httpContext.User.FindFirstValue(ClaimTypes.Name),
            UserEmail = httpContext.User.FindFirstValue(ClaimTypes.Email),
            ResourceId = resourceId,
            ResourceType = resourceType,
            FilePermissions = filePermissions,
            ContainerPermissions = containerPermissions,
        };
        var token = await accessTokenService.IssueAsync(request, cancellationToken).ConfigureAwait(false);
        return token.Token;
    }

    /// <summary>
    /// Parses the <c>X-WOPI-WopiSrc</c> header into a <see cref="WopiResourceType"/> and the
    /// resource identifier. Accepts absolute URIs whose path ends with <c>/files/{id}</c> or
    /// <c>/containers/{id}</c> (case-insensitive). When the path contains multiple candidate
    /// segments — e.g. <c>/files/archive/containers/abc</c> — the trailing pair wins, which
    /// matches the WOPI spec's "the resource is at the URL tail" intent and avoids the
    /// first-match-wins ambiguity of the previous segment-scan implementation.
    /// </summary>
    public static bool TryParseWopiSrc(string wopiSrc, out WopiResourceType resourceType, out string resourceId)
    {
        resourceType = default;
        resourceId = string.Empty;

        if (string.IsNullOrWhiteSpace(wopiSrc) || !Uri.TryCreate(wopiSrc, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var match = WopiSrcPathRegex().Match(uri.AbsolutePath);
        if (!match.Success)
        {
            return false;
        }

        resourceType = match.Groups[1].Value.Equals("files", StringComparison.OrdinalIgnoreCase)
            ? WopiResourceType.File
            : WopiResourceType.Container;
        resourceId = Uri.UnescapeDataString(match.Groups[2].Value);
        return true;
    }
}
