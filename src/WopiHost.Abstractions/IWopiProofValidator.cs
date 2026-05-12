using Microsoft.AspNetCore.Http;

namespace WopiHost.Abstractions;

/// <summary>
/// Validates the WOPI proof headers on an incoming request to confirm it originated from a
/// trusted WOPI client (typically Office Online / M365 for the Web).
/// </summary>
/// <remarks>
/// <para>
/// Proof keys are an Office-Online-family feature — Microsoft signs every WOPI callback with
/// one of two RSA keys advertised in the host-discovery XML. The default implementation
/// registered by <c>AddWopi()</c> verifies that signature against the keys returned by the
/// sibling discovery service.
/// </para>
/// <para>
/// Replace via DI when running against a WOPI client that doesn't sign callbacks with proof
/// keys (e.g. Collabora Online), or to layer additional checks. The sample uses
/// <c>services.RemoveAll&lt;IWopiProofValidator&gt;()</c> followed by a no-op registration
/// when <c>Wopi:Security:DisableProofValidation</c> is set in Development.
/// </para>
/// </remarks>
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
