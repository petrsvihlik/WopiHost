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
        var routePath = linkGenerator.GetPathByName(httpContext, routeName, values)
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