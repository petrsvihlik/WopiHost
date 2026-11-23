using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WopiHost.Abstractions;

namespace WopiHost.Web.Oidc.Infrastructure;

/// <summary>
/// Represents the inputs for minting a WOPI access token. Optional fields are nullable.
/// </summary>
public sealed record WopiTokenMintRequest
{
    /// <summary>Gets the stable unique id of the user (typically the OIDC <c>sub</c> claim).</summary>
    public required string UserId { get; init; }

    /// <summary>Gets the friendly display name. When <see langword="null"/>, the issued <c>name</c> claim falls back to <see cref="UserId"/>.</summary>
    public string? UserDisplayName { get; init; }

    /// <summary>Gets the user's email, if available. Omitted from the token when <see langword="null"/> or empty.</summary>
    public string? UserEmail { get; init; }

    /// <summary>Gets the identifier of the WOPI resource the token is bound to.</summary>
    public required string ResourceId { get; init; }

    /// <summary>Gets the WOPI permissions baked into the token. <see cref="WopiFilePermissions.None"/> omits the claim.</summary>
    public WopiFilePermissions FilePermissions { get; init; } = WopiFilePermissions.None;

    /// <summary>Gets the token lifetime. Defaults to 10 minutes when <see langword="null"/>.</summary>
    public TimeSpan? Lifetime { get; init; }
}

/// <summary>
/// Mints WOPI access tokens that the WOPI client (Office Online) replays back to the WOPI
/// backend. Format and signing key MUST match the WOPI server's
/// <c>JwtAccessTokenService</c>, configured in <c>sample/WopiHost/Program.cs</c>.
/// </summary>
/// <remarks>
/// In production, host both servers in the same trust boundary (one process, or a shared
/// secret store) and derive this key from a managed secret, not a string constant.
/// </remarks>
public sealed class WopiAccessTokenMinter
{
    /// <summary>Specifies the demo-only secret. Both this sample and <c>sample/WopiHost</c> default to this string.</summary>
    public const string DefaultDevSecret = "wopi-sample-shared-dev-key";

    private readonly byte[] _signingKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="WopiAccessTokenMinter"/> class with the supplied
    /// raw HMAC key, zero-padded on the right to 32 bytes when shorter.
    /// </summary>
    /// <param name="signingKey">The raw HMAC-SHA256 signing key.</param>
    public WopiAccessTokenMinter(byte[] signingKey)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        _signingKey = signingKey.Length >= 32 ? signingKey : Pad(signingKey);
    }

    /// <summary>Creates a minter whose HMAC key is the UTF-8 encoding of <paramref name="secret"/>.</summary>
    /// <param name="secret">The shared secret to derive the key from.</param>
    /// <returns>A new <see cref="WopiAccessTokenMinter"/>.</returns>
    public static WopiAccessTokenMinter FromSecret(string secret) =>
        new(Encoding.UTF8.GetBytes(secret));

    /// <summary>Issues a WOPI access token bound to a resource and user.</summary>
    /// <param name="request">The resource, user and permissions to bind into the token.</param>
    /// <returns>The serialized token and its expiry.</returns>
    public (string Token, DateTimeOffset ExpiresAt) Mint(WopiTokenMintRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.UserId);
        ArgumentException.ThrowIfNullOrEmpty(request.ResourceId);

        var expires = DateTimeOffset.UtcNow.Add(request.Lifetime ?? TimeSpan.FromMinutes(10));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, request.UserId),
            new(ClaimTypes.Name, request.UserDisplayName ?? request.UserId),
            new(WopiClaimTypes.ResourceId, request.ResourceId),
            new(WopiClaimTypes.ResourceType, WopiResourceType.File.ToString()),
        };
        if (!string.IsNullOrEmpty(request.UserDisplayName))
        {
            claims.Add(new Claim(WopiClaimTypes.UserDisplayName, request.UserDisplayName));
        }
        if (!string.IsNullOrEmpty(request.UserEmail))
        {
            claims.Add(new Claim(ClaimTypes.Email, request.UserEmail));
        }
        if (request.FilePermissions != WopiFilePermissions.None)
        {
            claims.Add(new Claim(WopiClaimTypes.FilePermissions, request.FilePermissions.ToString()));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = DateTime.UtcNow,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_signingKey), SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return (handler.WriteToken(handler.CreateToken(descriptor)), expires);
    }

    private static byte[] Pad(byte[] raw)
    {
        var padded = new byte[32];
        Array.Copy(raw, padded, raw.Length);
        return padded;
    }
}
