using System.Diagnostics.CodeAnalysis;
using Microsoft.IdentityModel.Tokens;

namespace WopiHost.Core.Security;

/// <summary>
/// Represents the options for the WOPI access-token signing pipeline used by the default
/// <see cref="Authentication.JwtAccessTokenService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Bind from configuration via <c>services.Configure&lt;WopiSecurityOptions&gt;(config.GetSection(WopiSecurityOptions.SectionName))</c>
/// or configure inline via the <c>AddWopi(o =&gt; ...)</c> overload.
/// </para>
/// <para>
/// In production, supply <see cref="SigningKey"/> from a managed secret store (Key Vault, Secrets Manager,
/// Data Protection-protected configuration). Never commit a signing key to source.
/// </para>
/// </remarks>
public class WopiSecurityOptions
{
    /// <summary>
    /// The default configuration section path these options bind to, for use with
    /// <c>builder.Configuration.GetSection(WopiSecurityOptions.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Wopi:Security";

    /// <summary>
    /// Gets or sets the HMAC signing key bytes. Must be at least 32 bytes (256 bits) with the default
    /// <see cref="SecurityAlgorithms.HmacSha256"/> algorithm.
    /// </summary>
    /// <remarks>
    /// When neither this nor <see cref="SecurityKey"/> is set, <see cref="Authentication.JwtAccessTokenService"/>
    /// generates a random per-process key on first use and logs a warning. That suits local
    /// development only: tokens are invalidated on every restart and cannot work across multiple
    /// host instances.
    /// </remarks>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays",
        Justification = "Bound from a base64 config string by the IConfiguration BinaryConverter, which only targets byte[].")]
    public byte[]? SigningKey { get; set; }

    /// <summary>
    /// Gets the additional <i>previous</i> signing keys accepted for validation only.
    /// </summary>
    /// <remarks>
    /// During key rotation, put the new key in <see cref="SigningKey"/>, keep the old key here for the
    /// duration of the longest token TTL, then remove it.
    /// </remarks>
    public IList<byte[]> AdditionalValidationKeys { get; } = [];

    /// <summary>
    /// Gets or sets the JWT signing algorithm. The default is <see cref="SecurityAlgorithms.HmacSha256"/>.
    /// For an asymmetric algorithm (e.g. <see cref="SecurityAlgorithms.RsaSha256"/>), supply an
    /// asymmetric key via <see cref="SecurityKey"/> and override this value.
    /// </summary>
    public string SigningAlgorithm { get; set; } = SecurityAlgorithms.HmacSha256;

    /// <summary>
    /// Gets or sets the optional issuer (<c>iss</c>) claim. When set, validation enforces it.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the optional audience (<c>aud</c>) claim. When set, validation enforces it.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of newly issued tokens when the caller does not specify one in
    /// <see cref="Abstractions.WopiAccessTokenRequest.Lifetime"/>. The default is 10 minutes.
    /// </summary>
    /// <remarks>
    /// The WOPI spec recommends short-lived tokens (~10 minutes). The host returns the expiry
    /// as <c>access_token_ttl</c> so the WOPI client can refresh in time.
    /// </remarks>
    public TimeSpan DefaultTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the clock skew tolerated when validating <c>nbf</c>/<c>exp</c>. The default is 30 seconds.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the optional key for non-HMAC signing scenarios. When set, it takes precedence
    /// over <see cref="SigningKey"/>.
    /// </summary>
    public SecurityKey? SecurityKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the composition root should replace
    /// <see cref="Abstractions.IWopiProofValidator"/> with a no-op. <b>Development only.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The library does not act on this flag; the hosting application reads it and swaps the
    /// validator. It exists for WOPI clients (notably Collabora Online) that do not sign callbacks
    /// with proof keys, an OOS / Microsoft 365 for the web feature.
    /// </para>
    /// <para>
    /// The sample host refuses to honor this flag outside the <c>Development</c> environment so a
    /// stray production config cannot silently disable signature checking. Hosts reading this
    /// property should enforce the same invariant.
    /// </para>
    /// </remarks>
    public bool DisableProofValidation { get; set; }
}
