using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using WopiHost.Abstractions;

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
    /// so the literal "files" / "containers" match is locale-independent (the Turkish dotless-I
    /// would otherwise break it).
    /// </summary>
    [GeneratedRegex(@"/(files|containers)/([^/?#]+)/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WopiSrcPathRegex();

    /// <summary>
    /// Enforces the WOPI "exactly one of header A or header B" contract used by name-negotiation
    /// endpoints (PutRelativeFile, CreateChildFile, CreateChildContainer). The spec mandates
    /// <c>501 Not Implemented</c> — not 400 — when both headers are present or both are absent,
    /// because the host has not chosen which negotiation mode (suggested vs specific) to support.
    /// Returns <see langword="null"/> when exactly one is set so the call site can proceed; the
    /// caller dispatches on which one was set via subsequent <c>!= null</c> checks.
    /// </summary>
    /// <remarks>
    /// Whitespace-only header values count as absent (<c>string.IsNullOrWhiteSpace</c> semantics).
    /// .NET 10's <c>Microsoft.Extensions.Validation</c> doesn't fit this seam: the validation
    /// pipeline is 400-shaped only and can't express 501 NI.
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
    /// matches the WOPI spec's "the resource is at the URL tail" intent and avoids
    /// first-match-wins ambiguity.
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
