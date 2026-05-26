using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;

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
    /// Builds a <see cref="WopiAccessTokenRequest"/> populated from the user's identity claims
    /// and the supplied resource identity + permissions. Synchronous on purpose — used at the
    /// call sites that previously routed through async helpers (<c>IssueAccessTokenForFileAsync</c>,
    /// <c>IssueAccessTokenForContainerAsync</c>, <c>IssueEcosystemPointerAsync</c>), which tripped
    /// an Infer# null-deref FP through the static-method-await indirection (#471 / pre-#363
    /// pattern). The await stays direct on the injected <see cref="IWopiAccessTokenService"/>
    /// so Infer# sees through it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Why mint a fresh token at all.</strong> Every endpoint that surfaces a child or
    /// ancestor URL (EnumerateAncestors, EnumerateChildren, PutRelativeFile, CreateChildFile,
    /// CreateChildContainer, ecosystem_pointer) builds the URL with a token bound to the *new*
    /// resource id — reusing the inbound token would either fail downstream authorization or
    /// open a token-trading hole per
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/security#preventing-token-trading"/>.
    /// </para>
    /// <para>
    /// <see cref="WopiAccessTokenRequest"/> exposes both <see cref="WopiAccessTokenRequest.FilePermissions"/>
    /// and <see cref="WopiAccessTokenRequest.ContainerPermissions"/> as init-only properties; the
    /// token-issuing path consults the set whose <see cref="WopiAccessTokenRequest.ResourceType"/>
    /// matches and ignores the other. Defaulting both to <see cref="WopiFilePermissions.None"/> /
    /// <see cref="WopiContainerPermissions.None"/> is the idiomatic shape.
    /// </para>
    /// </remarks>
    public static WopiAccessTokenRequest BuildResourceTokenRequest(
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

    /// <summary>
    /// Enforces the WOPI "exactly one of header A or header B" contract used by name-negotiation
    /// endpoints (PutRelativeFile, CreateChildFile, CreateChildContainer). The spec mandates
    /// <c>501 Not Implemented</c> — not 400 — when both headers are present or both are absent,
    /// because the host has not chosen which negotiation mode (suggested vs specific) to support.
    /// Returns <see langword="null"/> when exactly one is set so the call site can proceed; the
    /// caller dispatches on which one was set via subsequent <c>!= null</c> checks.
    /// </summary>
    /// <remarks>
    /// Whitespace-only header values count as absent (matches the
    /// <c>string.IsNullOrWhiteSpace</c> semantics the hand-rolled call sites used). .NET 10's
    /// <c>Microsoft.Extensions.Validation</c> doesn't fit this seam — see #466 for the
    /// investigation; the validation pipeline is 400-shaped only and can't express 501 NI.
    /// </remarks>
    public static StatusCodeHttpResult? EnsureExactlyOneOf(string? a, string? b)
        => string.IsNullOrWhiteSpace(a) == string.IsNullOrWhiteSpace(b)
            ? TypedResults.StatusCode(StatusCodes.Status501NotImplemented)
            : null;

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
