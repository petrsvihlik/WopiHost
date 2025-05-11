using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="HttpRequest"/>.
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// Gets the access token from the request in various formats (query string, form data, or authorization header).
    /// </summary>
    /// <remarks>
    /// According to Microsoft documentation:
    /// "WOPI clients aren't required to pass the access token in the Authorization header, but they must send it as a URL parameter in all WOPI operations.
    /// So, for maximum compatibility, WOPI hosts should either use the URL parameter in all cases, or fall back to it if the Authorization header isn't included in the request."
    /// 
    /// This method follows this guidance by first checking the URL parameter (query string), then form data, and finally falling back to the Authorization header.
    /// </remarks>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The access token if found; otherwise, an empty string.</returns>
    public static string GetAccessToken(this HttpRequest request)
    {
        // First try to get from query string
        if (request.Query.TryGetValue(AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME, out StringValues tokenFromQuery) && 
            !StringValues.IsNullOrEmpty(tokenFromQuery))
        {
            return tokenFromQuery.ToString();
        }
        
        // Then try to get from form data in POST requests
        if (request.HasFormContentType && 
            request.Form.TryGetValue(AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME, out StringValues tokenFromForm) && 
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
    /// Proxy-aware construction of URL request.
    /// </summary>
    public static string GetProxyAwareRequestUrl(this HttpRequest request)
    {
        var urlPart = request.GetProxyAwareUrlParts();
        
        return $"{urlPart.scheme}://{urlPart.host}{urlPart.pathBase}{urlPart.path}{urlPart.queryString}";
    }

    /// <summary>
    /// Get's the Uri parts to construct URL's.
    /// </summary>
    public static (string? scheme, string? host, string? pathBase, string? path, string? queryString)
        GetProxyAwareUrlParts(this HttpRequest request)
    {
        var scheme = request.Headers.ContainsKey("X-Forwarded-Proto") 
            ? request.Headers["X-Forwarded-Proto"].ToString() 
            : request.Scheme;
        
        var host = request.Headers.ContainsKey("X-Forwarded-Host") 
            ? request.Headers["X-Forwarded-Host"].ToString() 
            : request.Host.Value;
        
        var pathBase = request.Headers.ContainsKey("X-Forwarded-PathBase") 
            ? request.Headers["X-Forwarded-PathBase"].ToString() 
            : request.PathBase.Value;
        
        var path = request.Path.Value;
        var queryString = request.QueryString.Value;
        
        return (scheme, host, pathBase, path, queryString);
    }
} 