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
    /// <param name="input">Stream to read from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Byte array copy of a stream.</returns>
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
    /// <param name="input">Stream to read from.</param>
    /// <param name="maxBytes">Maximum number of bytes to accept.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>WithinLimit</c> is <c>true</c> with the body bytes (length &lt;= <paramref name="maxBytes"/>)
    /// when the stream stayed within the cap; <c>false</c> with an empty array when it yielded more
    /// than <paramref name="maxBytes"/> bytes. A value-tuple rather than a nullable array so the
    /// result never carries a null that callers (or static analysis) have to reason about.
    /// </returns>
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
    /// Tries to parse integer from string. Returns null if parsing fails.
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <returns>Integer parsed from <paramref name="s"/></returns>
    public static int? ToNullableInt(this string s)
    {
        if (int.TryParse(s, out var i)) return i;
        return null;
    }

    /// <summary>
    /// Converts <see cref="DateTime"/> to UNIX timestamp.
    /// </summary>
    public static long ToUnixTimestamp(this DateTime dateTime)
    {
        DateTimeOffset dto = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return dto.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Returns the User NameIdentifier claim value.
    /// </summary>
    /// <param name="principal">the current user</param>
    /// <returns>nameIdentifier</returns>
    /// <exception cref="InvalidOperationException">if such a claim does not exist</exception>
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.ToSafeIdentity()
            ?? throw new InvalidOperationException("Could not find NameIdentifier claim");
    }

    /// <summary>
    /// Replaces forbidden characters in identity properties with an underscore.
    /// Accordingly to: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#requirements-for-user-identity-properties
    /// </summary>
    /// <param name="identity">Identity property value</param>
    /// <returns>String safe to use as an identity property</returns>
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
    /// <param name="httpContext">Current HTTP context.</param>
    /// <param name="routeName">Name of the named route from <see cref="WopiRouteNames"/>.</param>
    /// <param name="identifier">Resource identifier (slotted into the <c>{id}</c> route value).</param>
    /// <param name="accessToken">Access token to embed. If null/empty, the inbound request's token is reused — the access-token query parameter must be present on the URL for the WOPI client to authenticate the callback.</param>
    /// <returns>https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#wopisrc</returns>
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
        // tests and future third-party providers. Same precedent as
        // DefaultCheckFileInfoBuilder's FileUrl construction.
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
    /// Typed file overload — preferred when the caller already holds an <see cref="IWopiFile"/>
    /// (resource kind comes from the static type, no runtime switch needed).
    /// </summary>
    public static Uri GetWopiSrc(this HttpContext httpContext, IWopiFile file, string? accessToken = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        return httpContext.GetWopiSrc(WopiRouteNames.CheckFileInfo, file.Identifier, accessToken);
    }

    /// <summary>
    /// Typed container overload — preferred when the caller already holds an
    /// <see cref="IWopiContainer"/>.
    /// </summary>
    public static Uri GetWopiSrc(this HttpContext httpContext, IWopiContainer container, string? accessToken = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        return httpContext.GetWopiSrc(WopiRouteNames.CheckContainerInfo, container.Identifier, accessToken);
    }

    /// <summary>
    /// Enum-dispatched overload — kept for the bootstrap / attribute paths where only the enum
    /// value is available. Prefer the typed file / container overloads when an instance is in scope.
    /// </summary>
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
    /// <param name="httpContext">HTTP context</param>
    /// <param name="file">the existing WopiFile</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    public static async Task CopyToWriteStream(this HttpContext httpContext, IWopiWritableFile file, CancellationToken cancellationToken = default)
    {
        using var stream = await file.OpenWriteAsync(cancellationToken).ConfigureAwait(false);
        await httpContext.Request.Body.CopyToAsync(
            stream,
            cancellationToken).ConfigureAwait(false);
    }
}