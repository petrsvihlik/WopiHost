using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WopiHost.Abstractions;

namespace WopiHost.Web.Oidc.Infrastructure;

/// <summary>
/// Mints WOPI access tokens that the WOPI client (Office Online) replays back to the WOPI
/// backend. Format and signing key MUST match the WOPI server's
/// <c>JwtAccessTokenService</c> — see <c>sample/WopiHost/Program.cs</c>.
/// </summary>
/// <remarks>
/// In production, host both servers in the same trust boundary (one process, or a shared
/// secret store) and derive this key from a managed secret, not a string constant.
/// </remarks>
public sealed class WopiAccessTokenMinter
{
    /// <summary>Demo-only secret. Both this sample and <c>sample/WopiHost</c> default to this string.</summary>
    public const string DefaultDevSecret = "wopi-sample-shared-dev-key";

    private readonly byte[] _signingKey;

    /// <summary>Creates a minter with the supplied raw HMAC key (right-padded to 32 bytes).</summary>
    public WopiAccessTokenMinter(byte[] signingKey)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        _signingKey = signingKey.Length >= 32 ? signingKey : Pad(signingKey);
    }

    /// <summary>Convenience constructor that derives the HMAC key from a string.</summary>
    public static WopiAccessTokenMinter FromSecret(string secret) =>
        new(Encoding.UTF8.GetBytes(secret));

    /// <summary>
    /// Issues a 10-minute WOPI access token bound to <paramref name="resourceId"/> with the
    /// supplied permissions and user identity.
    /// </summary>
    public (string Token, DateTimeOffset ExpiresAt) Mint(
        string userId,
        string? userDisplayName,
        string? userEmail,
        string resourceId,
        WopiFilePermissions filePermissions,
        TimeSpan? lifetime = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(resourceId);

        var expires = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(10));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userDisplayName ?? userId),
            new(WopiClaimTypes.ResourceId, resourceId),
            new(WopiClaimTypes.ResourceType, WopiResourceType.File.ToString()),
        };
        if (!string.IsNullOrEmpty(userDisplayName))
        {
            claims.Add(new Claim(WopiClaimTypes.UserDisplayName, userDisplayName));
        }
        if (!string.IsNullOrEmpty(userEmail))
        {
            claims.Add(new Claim(ClaimTypes.Email, userEmail));
        }
        if (filePermissions != WopiFilePermissions.None)
        {
            claims.Add(new Claim(WopiClaimTypes.FilePermissions, filePermissions.ToString()));
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
