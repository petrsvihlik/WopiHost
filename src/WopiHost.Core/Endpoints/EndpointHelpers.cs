using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using WopiHost.Abstractions;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Provides helpers shared across the WOPI Minimal-API endpoint handlers.
/// </summary>
/// <remarks>
/// Holds only cross-resource logic; single-resource concerns live in their respective endpoint file.
/// </remarks>
internal static partial class EndpointHelpers
{
    /// <summary>
    /// Gets a regex that matches a trailing <c>/files/{id}</c> or <c>/containers/{id}</c> pair in a path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anchored at the end so that a host serving WOPI under a parent path that contains a segment
    /// named <c>files</c> or <c>containers</c> (e.g. <c>/repository/files/wopi/containers/abc</c>)
    /// still resolves to the WOPI resource at the tail. The <c>[^/?#]+</c> character class is
    /// defensive: <see cref="Uri.AbsolutePath"/> never includes a query or fragment.
    /// </para>
    /// <para>
    /// <see cref="RegexOptions.CultureInvariant"/> pairs with <see cref="RegexOptions.IgnoreCase"/>
    /// so the literal <c>files</c> / <c>containers</c> match is locale-independent (the Turkish
    /// dotless I would otherwise break it).
    /// </para>
    /// </remarks>
    [GeneratedRegex(@"/(files|containers)/([^/?#]+)/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WopiSrcPathRegex();

    /// <summary>
    /// Enforces the WOPI "exactly one of header A or header B" contract used by the name-negotiation
    /// endpoints (PutRelativeFile, CreateChildFile, CreateChildContainer).
    /// </summary>
    /// <param name="a">The value of the first header.</param>
    /// <param name="b">The value of the second header.</param>
    /// <returns>
    /// A <c>501 Not Implemented</c> result when both headers are present or both are absent;
    /// <see langword="null"/> when exactly one is set and the caller can proceed.
    /// </returns>
    /// <remarks>
    /// The spec mandates <c>501</c>, not <c>400</c>, for the both/neither case because the host has not
    /// chosen which negotiation mode (suggested or specific) to support. Whitespace-only header values
    /// count as absent (<see cref="string.IsNullOrWhiteSpace(string)"/> semantics).
    /// </remarks>
    public static StatusCodeHttpResult? EnsureExactlyOneOf(string? a, string? b)
        => string.IsNullOrWhiteSpace(a) == string.IsNullOrWhiteSpace(b)
            ? TypedResults.StatusCode(StatusCodes.Status501NotImplemented)
            : null;

    /// <summary>
    /// Tries to parse the <c>X-WOPI-WopiSrc</c> header into a <see cref="WopiResourceType"/> and a
    /// resource identifier.
    /// </summary>
    /// <param name="wopiSrc">The header value; an absolute URI whose path ends with <c>/files/{id}</c> or <c>/containers/{id}</c> (case-insensitive).</param>
    /// <param name="resourceType">When this method returns <see langword="true"/>, the type of the referenced resource.</param>
    /// <param name="resourceId">When this method returns <see langword="true"/>, the unescaped resource identifier; otherwise an empty string.</param>
    /// <returns><see langword="true"/> if <paramref name="wopiSrc"/> was parsed; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// When the path contains multiple candidate segments (e.g. <c>/files/archive/containers/abc</c>),
    /// the trailing pair wins: the WOPI resource is at the URL tail.
    /// </remarks>
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
