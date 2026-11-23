using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Represents a framework-neutral envelope for an inbound WOPI request. The <c>WopiHost.Core</c> layer
/// constructs one of these from <c>HttpContext</c> before invoking
/// <see cref="ICheckFileInfoBuilder"/> / <see cref="IWopiProofValidator"/>; the
/// <c>WopiHost.Abstractions</c> library itself stays free of any HTTP-framework type so
/// replacement implementations can be authored without depending on ASP.NET.
/// </summary>
/// <remarks>
/// <para>
/// Two of the four customisation seams (<see cref="ICheckContainerInfoBuilder"/> and
/// <see cref="ICheckFolderInfoBuilder"/>) only need <see cref="User"/> and take a plain
/// <see cref="ClaimsPrincipal"/> directly. The two that also need URL / header data
/// (<see cref="ICheckFileInfoBuilder"/>, for custom builders that derive URLs from the request;
/// <see cref="IWopiProofValidator"/> for the signed-payload reconstruction) take this record.
/// </para>
/// <para>
/// <see cref="GetHeader"/> is a delegate rather than a snapshotted
/// <see cref="System.Collections.Generic.IReadOnlyDictionary{TKey,TValue}"/> so the adapter
/// can read directly from the underlying header source without materialising a copy on every
/// request. Implementations MUST be case-insensitive (HTTP header semantics).
/// </para>
/// </remarks>
public sealed record WopiRequestInfo
{
    /// <summary>
    /// Gets the authenticated principal for the request. May be an anonymous
    /// <see cref="ClaimsPrincipal"/> when the consumer runs before the auth pipeline (e.g.
    /// <see cref="IWopiProofValidator"/>, which gates the request <i>before</i> the
    /// access-token handler has materialised an identity).
    /// </summary>
    public required ClaimsPrincipal User { get; init; }

    /// <summary>
    /// Gets the proxy-aware absolute request URL — scheme + host + path + query as seen upstream of any
    /// reverse proxy (i.e. <c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c> /
    /// <c>X-Forwarded-PathBase</c> honoured). Used by <see cref="IWopiProofValidator"/> for the
    /// signed-payload reconstruction; available to custom <see cref="ICheckFileInfoBuilder"/>
    /// implementations that build self-referential URLs.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> when the adapter could not reconstruct a well-formed absolute
    /// URL — happens in tests that build a bare <c>DefaultHttpContext</c> without populating
    /// scheme + host, and in any synthetic-request scenario the framework adapter can't
    /// resolve. Consumers MUST null-check: <see cref="IWopiProofValidator"/> should treat
    /// <see langword="null"/> as "no URL to sign against → fail validation"; an <see cref="ICheckFileInfoBuilder"/>
    /// that derives URLs from the request should skip that construction.
    /// </remarks>
    public Uri? RequestUrl { get; init; }

    /// <summary>
    /// Gets the WOPI <c>access_token</c> resolved from the request (query string, form, or
    /// <c>Authorization: Bearer</c>), or <see langword="null"/> when the request doesn't
    /// carry one. Pre-resolved by the adapter so consumers don't have to know about the
    /// fallback order or the underlying HTTP-framework primitives.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Gets the case-insensitive header lookup, which returns <see langword="null"/> for absent headers.
    /// Implementations MUST treat the key as case-insensitive (HTTP header semantics).
    /// </summary>
    public required Func<string, string?> GetHeader { get; init; }
}
