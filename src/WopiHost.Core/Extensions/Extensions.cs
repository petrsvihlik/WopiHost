using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Security.Authentication;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Extensions;

internal static class Extensions
{
    /// <summary>
    /// Copies the stream to a byte array.
    /// </summary>
    /// <param name="input">Stream to read from</param>
    /// <returns>Byte array copy of a stream</returns>
    public static async Task<byte[]> ReadBytesAsync(this Stream input)
    {
        using var ms = new MemoryStream();
        await input.CopyToAsync(ms);
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
    /// Get WOPI authentication token
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    private static string? GetAccessToken(this HttpContext httpContext)
    {
        //TODO: an alternative would be HttpContext.GetTokenAsync(AccessTokenDefaults.AuthenticationScheme, AccessTokenDefaults.AccessTokenQueryName).Result (if the code below doesn't work)
        var authenticateInfo = httpContext.AuthenticateAsync(AccessTokenDefaults.AUTHENTICATION_SCHEME).Result;
        return authenticateInfo?.Properties?.GetTokenValue(AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME);
    }

    /// <summary>
    /// Creates an absolute URL to access a WOPI object of choice.
    /// </summary>
    /// <param name="url">url helper</param>
    /// <param name="routeName">Name of the route to be called from <see cref="WopiRouteNames"/>.</param>
    /// <param name="identifier">Identifier of an object associated to the controller.</param>
    /// <param name="accessToken">Access token to use for authentication for the given controller.</param>
    /// <returns>https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#wopisrc</returns>
    public static string GetWopiSrc(this IUrlHelper url, string routeName, string? identifier = null, string? accessToken = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeName);

        accessToken ??= url.ActionContext.HttpContext.GetAccessToken();
        
        return url.ProxyAwareRouteUrl(routeName, new { id = identifier ?? string.Empty, access_token = accessToken })
               ?? throw new InvalidOperationException(routeName + " route not found");
    }

    /// <summary>
    /// Creates an absolute URL to access a WOPI resource.
    /// </summary>
    /// <param name="url">url helper</param>
    /// <param name="resourceType">which Wopi resource to access</param>
    /// <param name="identifier">resource unique identifier</param>
    /// <param name="accessToken">Access token to use for authentication for the given controller.</param>
    /// <returns>https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#wopisrc</returns>
    public static string GetWopiSrc(this IUrlHelper url, WopiResourceType resourceType, string? identifier = null, string? accessToken = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.GetWopiSrc(
            resourceType switch
            {
                WopiResourceType.File => WopiRouteNames.CheckFileInfo,
                WopiResourceType.Container => WopiRouteNames.CheckContainerInfo,
                _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null)
            }, identifier, accessToken);
    }

    /// <summary>
    /// Checks if the resource has a specific permission as setup by <see cref="WopiAuthorizationHandler"/>
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <param name="permission">type of permission</param>
    /// <returns>true if permission found and allowed, false otherwise</returns>
    public static bool IsPermitted(this HttpContext httpContext, Permission permission)
    {
        return httpContext.Items.TryGetValue(permission, out var value) && value is bool boolValue && boolValue;
    }

    /// <summary>
    /// Copies the request body to the write stream of the file.
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <param name="file">the existing WopiFile</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    public static async Task CopyToWriteStream(this HttpContext httpContext, IWopiFile file, CancellationToken cancellationToken = default)
    {
        using var stream = await file.GetWriteStream(cancellationToken);
        await httpContext.Request.Body.CopyToAsync(
            stream,
            cancellationToken);
    }

    private static string? ProxyAwareRouteUrl(this IUrlHelper helper,
        string? routeName,
        object? values)
    {
        var urlPart = helper.ActionContext.HttpContext.Request.GetProxyAwareUrlParts();
        var routeUrl = helper.RouteUrl(routeName, values, urlPart.scheme);
        
        var uri = new Uri(routeUrl!);
        var pathBase = uri.AbsolutePath.EndsWith('/') && uri.AbsolutePath.Length > 1
            ? uri.AbsolutePath.Substring(0, uri.AbsolutePath.Length - 1)
            : uri.AbsolutePath;
        var queryString = uri.Query;

        return $"{urlPart.scheme}://{urlPart.host}{pathBase}{queryString}";
    }
}