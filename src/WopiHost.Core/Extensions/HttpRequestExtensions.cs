using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Extensions;

/// <summary>
/// Provides extension methods for <see cref="HttpRequest"/>.
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// Gets the access token from the request's query string, form data, or <c>Authorization</c> header.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The WOPI documentation states:
    /// "WOPI clients aren't required to pass the access token in the Authorization header, but they must send it as a URL parameter in all WOPI operations.
    /// So, for maximum compatibility, WOPI hosts should either use the URL parameter in all cases, or fall back to it if the Authorization header isn't included in the request."
    /// </para>
    /// <para>
    /// The query string is checked first, then form data, and finally a <c>Bearer</c> <c>Authorization</c> header.
    /// </para>
    /// </remarks>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The access token if found; otherwise, an empty string.</returns>
    public static string GetAccessToken(this HttpRequest request)
    {
        // First try to get from query string
        if (request.Query.TryGetValue(AccessTokenDefaults.AccessTokenQueryName, out StringValues tokenFromQuery) && 
            !StringValues.IsNullOrEmpty(tokenFromQuery))
        {
            return tokenFromQuery.ToString();
        }
        
        // Then try to get from form data in POST requests
        if (request.HasFormContentType && 
            request.Form.TryGetValue(AccessTokenDefaults.AccessTokenQueryName, out StringValues tokenFromForm) && 
            !StringValues.IsNullOrEmpty(tokenFromForm))
        {
            return tokenFromForm.ToString();
        }
        
        // Lastly check header (less common)
        if (request.Headers.TryGetValue("Authorization", out StringValues authHeader) && 
            !StringValues.IsNullOrEmpty(authHeader))
        {
            var header = authHeader.ToString();
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return header["Bearer ".Length..].Trim();
            }
        }
        
        return string.Empty;
    }
    
    /// <summary>
    /// Builds the absolute request URL, honoring the <c>X-Forwarded-Proto</c>, <c>X-Forwarded-Host</c>
    /// and <c>X-Forwarded-PathBase</c> headers.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The request URL as seen by the client, including the query string.</returns>
    public static string GetProxyAwareRequestUrl(this HttpRequest request)
    {
        var (scheme, host, pathBase, path, queryString) = request.GetProxyAwareUrlParts();
        
        return $"{scheme}://{host}{pathBase}{path}{queryString}";
    }

    /// <summary>
    /// Gets the URL parts of the request, honoring the <c>X-Forwarded-Proto</c>, <c>X-Forwarded-Host</c>
    /// and <c>X-Forwarded-PathBase</c> headers.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The scheme, host, path base, path, and query string of the request.</returns>
    public static (string? scheme, string? host, string? pathBase, string? path, string? queryString)
        GetProxyAwareUrlParts(this HttpRequest request)
    {
        var scheme = request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto)
            ? forwardedProto.ToString()
            : request.Scheme;

        var host = request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost)
            ? forwardedHost.ToString()
            : request.Host.Value;

        var pathBase = request.Headers.TryGetValue("X-Forwarded-PathBase", out var forwardedPathBase)
            ? forwardedPathBase.ToString()
            : request.PathBase.Value;
        
        var path = request.Path.Value;
        var queryString = request.QueryString.Value;
        
        return (scheme, host, pathBase, path, queryString);
    }
} 