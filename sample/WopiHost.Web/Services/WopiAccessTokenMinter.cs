using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WopiHost.Abstractions;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Web.Services;

/// <summary>
/// Anonymous-frontend token-minter. Lives in a service so the Razor Components page handler
/// (<c>Components/Pages/Detail.razor</c>) and any future API endpoint share the same claim
/// layout and signing key.
/// </summary>
/// <remarks>
/// The token format is a contract with the WopiHost server's <c>JwtAccessTokenService</c>: same
/// HMAC key, same claim layout. We sign the JWT inline here rather than depending on the
/// server's Core library so this sample stays a thin frontend (no controllers/auth pipeline).
/// </remarks>
public sealed class WopiAccessTokenMinter
{
    // Demo-only shared key — must match the WopiHost server's Wopi:Security:SigningKey.
    // In a real frontend, load this from the same managed secret store the server uses.
    private static readonly byte[] s_sharedSigningKey = DerivePaddedKey("wopi-sample-shared-dev-key");

    /// <summary>Mints a JWT scoped to <paramref name="action"/> for <paramref name="userId"/> on <paramref name="resourceId"/>.</summary>
    public (string Token, DateTimeOffset ExpiresAt) Mint(string userId, string resourceId, WopiActionEnum action)
    {
        // Scope permissions by action. OOS / M365 ship distinct view vs edit URLs (the URL alone
        // enforces the mode), but Collabora Online uses a single editor URL and derives the mode
        // from CheckFileInfo permission flags — so a token granting UserCanWrite always opens
        // in edit, regardless of which discovery action was selected. View-only must omit it.
        var perms = action == WopiActionEnum.Edit
            ? (WopiFilePermissions.UserCanWrite
               | WopiFilePermissions.UserCanRename
               | WopiFilePermissions.UserCanAttend
               | WopiFilePermissions.UserCanPresent)
            : WopiFilePermissions.UserCanAttend;

        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        var descriptor = new SecurityTokenDescriptor
        {
            // Claims must match the layout the WopiHost server's JwtAccessTokenService writes.
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userId),
                new Claim("wopi:rid", resourceId),
                new Claim("wopi:rtype", "File"),
                new Claim("wopi:fperms", perms.ToString()),
            ]),
            NotBefore = DateTime.UtcNow,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(s_sharedSigningKey), SecurityAlgorithms.HmacSha256),
        };
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return (handler.WriteToken(handler.CreateToken(descriptor)), expires);
    }

    private static byte[] DerivePaddedKey(string secret)
    {
        var raw = Encoding.UTF8.GetBytes(secret);
        if (raw.Length >= 32) return raw;
        var padded = new byte[32];
        Array.Copy(raw, padded, raw.Length);
        return padded;
    }
}
