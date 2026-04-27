using System.ComponentModel.DataAnnotations;
using Microsoft.IdentityModel.Tokens;

namespace WopiHost.Core.Security;

/// <summary>
/// Configuration for the WOPI access-token signing pipeline (used by the default
/// <see cref="Authentication.JwtAccessTokenService"/>).
/// </summary>
/// <remarks>
/// <para>
/// Bind from configuration via <c>services.Configure&lt;WopiSecurityOptions&gt;(config.GetSection("Wopi:Security"))</c>
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
    /// HMAC signing key bytes. Must be at least 32 bytes (256 bits) when using the default
    /// <see cref="SecurityAlgorithms.HmacSha256"/> algorithm.
    /// </summary>
    /// <remarks>
    /// If left unset when running in <c>Development</c>, the host generates a random per-process
    /// key on startup and logs a warning. This is convenient for local dev but means tokens are
    /// invalidated on every restart and cannot work across multiple host instances.
    /// </remarks>
    public byte[]? SigningKey { get; set; }

    /// <summary>
    /// Additional <em>previous</em> signing keys accepted for validation only. Use during key
    /// rotation: add the new key to <see cref="SigningKey"/>, leave the old key here for the
    /// duration of the longest token TTL, then remove it.
    /// </summary>
    public IList<byte[]> AdditionalValidationKeys { get; } = [];

    /// <summary>
    /// JWT signing algorithm. Defaults to <see cref="SecurityAlgorithms.HmacSha256"/>.
    /// Use an asymmetric algorithm (e.g. <see cref="SecurityAlgorithms.RsaSha256"/>) by
    /// supplying an asymmetric key via <see cref="SecurityKey"/> and overriding this value.
    /// </summary>
    public string SigningAlgorithm { get; set; } = SecurityAlgorithms.HmacSha256;

    /// <summary>
    /// Optional issuer (<c>iss</c>) claim. When set, the validator enforces it.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Optional audience (<c>aud</c>) claim. When set, the validator enforces it.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Default lifetime for newly issued tokens when the caller does not specify one in
    /// <see cref="Abstractions.WopiAccessTokenRequest.Lifetime"/>.
    /// </summary>
    /// <remarks>
    /// The WOPI spec recommends short-lived tokens (~10 minutes). The host returns the expiry
    /// as <c>access_token_ttl</c> so the WOPI client can refresh in time.
    /// </remarks>
    public TimeSpan DefaultTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Clock skew tolerated when validating <c>nbf</c>/<c>exp</c>. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Optional asymmetric key for non-HMAC signing scenarios. When set, takes precedence
    /// over <see cref="SigningKey"/>.
    /// </summary>
    public SecurityKey? SecurityKey { get; set; }
}
