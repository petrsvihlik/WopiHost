using Microsoft.AspNetCore.Http;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Interface for validating WOPI proof headers to ensure requests come from a trusted WOPI client.
/// </summary>
public interface IWopiProofValidator
{
    /// <summary>
    /// Validates the WOPI proof headers on the given request.
    /// </summary>
    /// <param name="httpContext">The HTTP request to validate.</param>
    /// <param name="accessToken">The access token from the request.</param>
    /// <returns>True if the request's proof headers are valid, false otherwise.</returns>
    Task<bool> ValidateProofAsync(HttpContext httpContext, string accessToken);
} 