using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Extensions;

internal static class Extensions
{
    /// <summary>
    /// Copies the stream to a byte array.
    /// </summary>
    /// <param name="input">The stream to read from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A byte array containing the remaining contents of <paramref name="input"/>.</returns>
    public static async Task<byte[]> ReadBytesAsync(this Stream input, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await input.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    /// <summary>
    /// Reads the stream into a byte array but bails out as soon as it sees more than
    /// <paramref name="maxBytes"/> bytes — so an oversized (or unbounded chunked) body can be
    /// rejected without ever buffering it in full. Prefer this over the unbounded
    /// <see cref="ReadBytesAsync(Stream, CancellationToken)"/> whenever the caller enforces a
    /// size cap (e.g. the spec-defined PutUserInfo limit).
    /// </summary>
    /// <param name="input">The stream to read from.</param>
    /// <param name="maxBytes">The maximum number of bytes to accept.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <c>WithinLimit</c> is <see langword="true"/> with the body bytes (length &lt;= <paramref name="maxBytes"/>)
    /// when the stream stayed within the cap; <see langword="false"/> with an empty array when it yielded more
    /// than <paramref name="maxBytes"/> bytes.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative.</exception>
    public static async Task<(bool WithinLimit, byte[] Bytes)> ReadBytesAsync(this Stream input, int maxBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);

        // Single backing buffer sized to maxBytes+1 so the read that crosses the cap trips the
        // limit — ReadAsync writes directly at the running offset, no intermediate chunk array
        // or MemoryStream copy, and the allocation can never exceed maxBytes+1.
        var buffer = new byte[maxBytes + 1];
        var total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                return (false, []);
            }
        }
        return (true, buffer[..total]);
    }

    /// <summary>
    /// Parses a string as an integer.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>The integer parsed from <paramref name="s"/>, or <see langword="null"/> if parsing fails.</returns>
    public static int? ToNullableInt(this string s)
    {
        if (int.TryParse(s, out var i)) return i;
        return null;
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> to a UNIX timestamp.
    /// </summary>
    /// <param name="dateTime">The value to convert. It is treated as UTC regardless of its <see cref="DateTime.Kind"/>.</param>
    /// <returns>The number of seconds elapsed since 1970-01-01T00:00:00Z.</returns>
    public static long ToUnixTimestamp(this DateTime dateTime)
    {
        DateTimeOffset dto = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return dto.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Gets the user's <see cref="ClaimTypes.NameIdentifier"/> claim value, sanitized with <see cref="ToSafeIdentity"/>.
    /// </summary>
    /// <param name="principal">The current user.</param>
    /// <returns>The sanitized name identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="principal"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="principal"/> has no <see cref="ClaimTypes.NameIdentifier"/> claim.</exception>
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.ToSafeIdentity()
            ?? throw new InvalidOperationException("Could not find NameIdentifier claim");
    }

    /// <summary>
    /// Replaces the characters forbidden in WOPI identity properties with an underscore.
    /// </summary>
    /// <param name="identity">The identity property value.</param>
    /// <returns>A string safe to use as an identity property.</returns>
    /// <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#requirements-for-user-identity-properties">Requirements for user identity properties</seealso>
    public static string ToSafeIdentity(this string identity)
    {
        const string forbiddenChars = "<>\"#{}^[]`\\/";
        return forbiddenChars.Aggregate(identity, (current, forbiddenChar) => current.Replace(forbiddenChar, '_'));
    }

    /// <summary>
    /// Builds the absolute URL of a named WOPI endpoint, signed with the request's access token
    /// (or with an explicit <paramref name="accessToken"/> override) and made proxy-aware via
    /// <see cref="HttpRequestExtensions.GetProxyAwareUrlParts"/>. Backed by
    /// <see cref="LinkGenerator"/> — no MVC URL-helper factory required.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="routeName">The name of the route, from <see cref="WopiRouteNames"/>.</param>
    /// <param name="identifier">The resource identifier, slotted into the <c>{id}</c> route value.</param>
    /// <param name="accessToken">The access token to embed. If <see langword="null"/> or empty, the inbound request's token is reused; the WOPI client needs the <c>access_token</c> query parameter on the URL to authenticate the callback.</param>
    /// <returns>The <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#wopisrc">WopiSrc</see> URL of the endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> or <paramref name="routeName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="routeName"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">No named route matches <paramref name="routeName"/>.</exception>
    public static Uri GetWopiSrc(this HttpContext httpContext, string routeName, string? identifier = null, string? accessToken = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeName);

        if (string.IsNullOrEmpty(accessToken))
        {
            // Reuse the request token rather than calling AuthenticateAsync().Result (deadlock risk).
            // HttpRequest.GetAccessToken probes query → form → Authorization: Bearer.
            var requestToken = httpContext.Request.GetAccessToken();
            accessToken = string.IsNullOrEmpty(requestToken) ? null : requestToken;
        }

        var linkGenerator = httpContext.RequestServices.GetRequiredService<LinkGenerator>();
        var values = new RouteValueDictionary
        {
            ["id"] = identifier ?? string.Empty,
            ["access_token"] = accessToken,
        };
        // Preserve identifier casing. The global routing option set by AddWopi()
        // (`AddRouting(o => o.LowercaseUrls = true)` for spec-compliant lowercase paths like
        // /wopi/files/{id} instead of /wopi/Files/{id}) lowercases BOTH route literals AND route
        // parameter values, but the
        // JWT `wopi:resource_id` claim is minted verbatim from file.Identifier /
        // container.Identifier. Without this override, a host with mixed-case ids (think a
        // SharePoint-style provider returning `01ABCDEF`) would build URLs containing
        // `01abcdef` while the embedded access token's claim says `01ABCDEF` — strict per-id
        // binding fails. Production SHA-256 ids are already lowercase so this matters most for
        // tests and future third-party providers.
        //
        // Trade-off: this override also stops lowercasing the route's LITERAL segments. That's
        // fine because every WOPI route is registered with already-lowercase literals
        // (`files`, `containers`, `folders`, `ecosystem`, `wopibootstrapper`) — see
        // MapWopiEndpoints. The lowercase-literal invariant is now load-bearing; a regression
        // test in WopiRouteNamesTests pins it so a future contributor adding `MapGet("/Files/...")`
        // would fail loudly rather than silently shipping URLs with mixed-case literals.
        var routePath = linkGenerator.GetPathByName(httpContext, routeName, values, options: new LinkOptions { LowercaseUrls = false })
            ?? throw new InvalidOperationException(routeName + " route not found");

        var request = httpContext.Request;
        var (scheme, host, forwardedPathBase, _, _) = request.GetProxyAwareUrlParts();
        // LinkGenerator.GetPathByName already prepends Request.PathBase. Only add the forwarded
        // path-base when it differs (e.g. proxy strips a prefix the app itself doesn't know about).
        var requestPathBase = request.PathBase.Value ?? string.Empty;
        var prefix = !string.IsNullOrEmpty(forwardedPathBase) && forwardedPathBase != requestPathBase
            ? forwardedPathBase
            : string.Empty;

        // RelativeOrAbsolute so the helper still works in test contexts (DefaultHttpContext with
        // empty Host) and behind proxies that strip the path-base. In production the result is
        // always absolute because Request.Scheme and Request.Host are populated by the host server.
        return new Uri($"{scheme}://{host}{prefix}{routePath}", UriKind.RelativeOrAbsolute);
    }

    /// <summary>
    /// Builds the WopiSrc URL of a file's <see cref="WopiRouteNames.CheckFileInfo"/> endpoint.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="file">The file to build the URL for.</param>
    /// <param name="accessToken">The access token to embed. If <see langword="null"/> or empty, the inbound request's token is reused.</param>
    /// <returns>The WopiSrc URL of <paramref name="file"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> or <paramref name="file"/> is <see langword="null"/>.</exception>
    public static Uri GetWopiSrc(this HttpContext httpContext, IWopiFile file, string? accessToken = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        return httpContext.GetWopiSrc(WopiRouteNames.CheckFileInfo, file.Identifier, accessToken);
    }

    /// <summary>
    /// Builds the WopiSrc URL of a container's <see cref="WopiRouteNames.CheckContainerInfo"/> endpoint.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="container">The container to build the URL for.</param>
    /// <param name="accessToken">The access token to embed. If <see langword="null"/> or empty, the inbound request's token is reused.</param>
    /// <returns>The WopiSrc URL of <paramref name="container"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> or <paramref name="container"/> is <see langword="null"/>.</exception>
    public static Uri GetWopiSrc(this HttpContext httpContext, IWopiContainer container, string? accessToken = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        return httpContext.GetWopiSrc(WopiRouteNames.CheckContainerInfo, container.Identifier, accessToken);
    }

    /// <summary>
    /// Builds the WopiSrc URL of a resource identified only by its <see cref="WopiResourceType"/>.
    /// The <see cref="IWopiFile"/> / <see cref="IWopiContainer"/> overloads are preferred when an instance is in scope.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="resourceType">The kind of resource.</param>
    /// <param name="identifier">The resource identifier.</param>
    /// <param name="accessToken">The access token to embed. If <see langword="null"/> or empty, the inbound request's token is reused.</param>
    /// <returns>The WopiSrc URL of the resource.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="resourceType"/> is not a defined value.</exception>
    public static Uri GetWopiSrc(this HttpContext httpContext, WopiResourceType resourceType, string? identifier = null, string? accessToken = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.GetWopiSrc(
            resourceType switch
            {
                WopiResourceType.File => WopiRouteNames.CheckFileInfo,
                WopiResourceType.Container => WopiRouteNames.CheckContainerInfo,
                // Exhaustive over WopiResourceType — extend both arms (and the typed overloads
                // above) when adding a new value.
                _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null),
            }, identifier, accessToken);
    }

    /// <summary>
    /// Copies the request body to the write stream of the file.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="file">The existing file to overwrite.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task CopyToWriteStream(this HttpContext httpContext, IWopiWritableFile file, CancellationToken cancellationToken = default)
    {
        using var stream = await file.OpenWriteAsync(cancellationToken).ConfigureAwait(false);
        await httpContext.Request.Body.CopyToAsync(
            stream,
            cancellationToken).ConfigureAwait(false);
    }
}