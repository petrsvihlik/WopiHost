using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using WopiHost.Abstractions;
using WopiHost.Web.Oidc.Infrastructure;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// Tests <see cref="WopiAccessTokenMinter"/>: token format, claim layout, signing-key derivation,
/// and round-trip validation with the same key. Pure-unit; no Docker required.
/// </summary>
public class WopiAccessTokenMinterTests
{
    // ReadJwtToken returns claims with their raw JWT payload names. The OutboundClaimTypeMap
    // converts ClaimTypes.NameIdentifier → "nameid", ClaimTypes.Email → "email",
    // ClaimTypes.Name → "unique_name" at minting time; the wopi:* names pass through as-is.
    private static readonly JwtSecurityTokenHandler Handler = new();

    [Fact]
    public void Mint_EmitsRequiredWopiClaims()
    {
        var minter = WopiAccessTokenMinter.FromSecret("test-secret");
        var (token, _) = minter.Mint("user-1", "Alice", "alice@example.com", "file-42",
            WopiFilePermissions.UserCanWrite);

        var jwt = Handler.ReadJwtToken(token);

        Assert.Equal("user-1", jwt.Claims.First(c => c.Type == "nameid").Value);
        Assert.Equal("Alice", jwt.Claims.First(c => c.Type == WopiClaimTypes.UserDisplayName).Value);
        Assert.Equal("alice@example.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Equal("file-42", jwt.Claims.First(c => c.Type == WopiClaimTypes.ResourceId).Value);
        Assert.Equal("File", jwt.Claims.First(c => c.Type == WopiClaimTypes.ResourceType).Value);
        Assert.Contains(WopiFilePermissions.UserCanWrite.ToString(),
            jwt.Claims.First(c => c.Type == WopiClaimTypes.FilePermissions).Value);
    }

    [Fact]
    public void Mint_OmitsEmailClaim_WhenEmailIsNull()
    {
        var minter = WopiAccessTokenMinter.FromSecret("test-secret");
        var (token, _) = minter.Mint("user-1", "Alice", userEmail: null, "file-42",
            WopiFilePermissions.UserCanWrite);

        var jwt = Handler.ReadJwtToken(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "email");
    }

    [Fact]
    public void Mint_OmitsPermissionsClaim_WhenNone()
    {
        var minter = WopiAccessTokenMinter.FromSecret("test-secret");
        var (token, _) = minter.Mint("user-1", "Alice", "alice@example.com", "file-42",
            WopiFilePermissions.None);

        var jwt = Handler.ReadJwtToken(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == WopiClaimTypes.FilePermissions);
    }

    [Fact]
    public void Mint_DefaultLifetime_IsTenMinutes()
    {
        var minter = WopiAccessTokenMinter.FromSecret("test-secret");
        var before = DateTimeOffset.UtcNow;
        var (_, expiresAt) = minter.Mint("u", "n", null, "r", WopiFilePermissions.UserCanWrite);
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(expiresAt, before.AddMinutes(10).AddSeconds(-1), after.AddMinutes(10).AddSeconds(1));
    }

    [Fact]
    public void Mint_TokenValidates_WithSameKey()
    {
        var secret = "test-secret-some-string";
        var minter = WopiAccessTokenMinter.FromSecret(secret);
        var (token, _) = minter.Mint("user-1", "Alice", "alice@example.com", "file-42",
            WopiFilePermissions.UserCanWrite);

        var keyBytes = PadToHmacKey(System.Text.Encoding.UTF8.GetBytes(secret));
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            NameClaimType = ClaimTypes.NameIdentifier,
        };

        // Use a validating handler with MapInboundClaims=true so "nameid" maps back to ClaimTypes.NameIdentifier.
        var validating = new JwtSecurityTokenHandler { MapInboundClaims = true };
        var principal = validating.ValidateToken(token, validationParameters, out _);
        Assert.Equal("user-1", principal.Identity!.Name);
    }

    [Fact]
    public void Mint_TokenRejected_WithDifferentKey()
    {
        var minter = WopiAccessTokenMinter.FromSecret("test-secret");
        var (token, _) = minter.Mint("user-1", "Alice", "alice@example.com", "file-42",
            WopiFilePermissions.UserCanWrite);

        var differentKey = PadToHmacKey(System.Text.Encoding.UTF8.GetBytes("a-totally-different-secret"));
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(differentKey),
        };

        Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out _));
    }

    [Fact]
    public void Mint_NullUserId_Throws()
    {
        var minter = WopiAccessTokenMinter.FromSecret("test-secret");
        // ArgumentException.ThrowIfNullOrEmpty throws ArgumentNullException for null, ArgumentException for empty.
        Assert.Throws<ArgumentNullException>(() => minter.Mint(null!, "n", null, "r", WopiFilePermissions.UserCanWrite));
    }

    private static byte[] PadToHmacKey(byte[] raw)
    {
        if (raw.Length >= 32) return raw;
        var padded = new byte[32];
        Array.Copy(raw, padded, raw.Length);
        return padded;
    }
}
