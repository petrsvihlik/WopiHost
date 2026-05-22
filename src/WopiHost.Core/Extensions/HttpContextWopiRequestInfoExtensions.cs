using Microsoft.AspNetCore.Http;
using WopiHost.Abstractions;

namespace WopiHost.Core.Extensions;

/// <summary>
/// Adapts an ASP.NET Core <see cref="HttpContext"/> into a framework-neutral
/// <see cref="WopiRequestInfo"/> for consumption by the Abstractions-layer customisation
/// seams (<see cref="ICheckFileInfoBuilder"/>, <see cref="IWopiProofValidator"/>).
/// </summary>
/// <remarks>
/// <para>
/// This adapter is the only place in the entire codebase that bridges
/// <see cref="HttpContext"/> to <see cref="WopiRequestInfo"/>. Keeping the bridge in a single
/// internal helper lets <c>WopiHost.Abstractions</c> stay free of any HTTP-framework
/// dependency — replacement <see cref="ICheckFileInfoBuilder"/> / <see cref="IWopiProofValidator"/>
/// implementations target <see cref="WopiRequestInfo"/> and never need to know about
/// <see cref="HttpContext"/>.
/// </para>
/// <para>
/// The proxy-aware URL is reconstructed from <c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c>
/// / <c>X-Forwarded-PathBase</c> headers so the signed-payload reconstruction in
/// <see cref="IWopiProofValidator"/> sees the same URL the WOPI client originally signed,
/// not the post-proxy hostname.
/// </para>
/// </remarks>
internal static class HttpContextWopiRequestInfoExtensions
{
    /// <summary>
    /// Builds a <see cref="WopiRequestInfo"/> from the supplied <see cref="HttpContext"/>.
    /// </summary>
    public static WopiRequestInfo ToWopiRequestInfo(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var request = httpContext.Request;
        // GetAccessToken returns string.Empty when absent; normalise to null so consumers
        // can null-check rather than IsNullOrEmpty-check (Abstractions doc says nullable).
        var token = request.GetAccessToken();

        // Synthesised contexts (tests, internal probes) can leave scheme/host blank, in which
        // case GetProxyAwareRequestUrl() returns "://" — not a valid absolute Uri. Fall back
        // to null so consumers (proof validator, FileUrl builder) skip cleanly rather than
        // throwing on Uri construction. Real WOPI requests always have a valid scheme + host.
        var rawUrl = request.GetProxyAwareRequestUrl();
        var requestUrl = Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed) ? parsed : null;

        return new WopiRequestInfo
        {
            User = httpContext.User,
            RequestUrl = requestUrl,
            AccessToken = string.IsNullOrEmpty(token) ? null : token,
            // IHeaderDictionary is case-insensitive — the delegate inherits that behaviour for
            // free. Returns null for absent headers per the WopiRequestInfo.GetHeader contract.
            GetHeader = name => request.Headers.TryGetValue(name, out var v) ? v.ToString() : null,
            RequestServices = httpContext.RequestServices,
        };
    }
}
