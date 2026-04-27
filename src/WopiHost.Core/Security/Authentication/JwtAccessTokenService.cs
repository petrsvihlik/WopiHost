using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Default <see cref="IWopiAccessTokenService"/> implementation. Issues signed JWTs that bind
/// a user to a single <c>(resource, permissions, lifetime)</c> tuple.
/// </summary>
public class JwtAccessTokenService : IWopiAccessTokenService
{
    private readonly IOptionsMonitor<WopiSecurityOptions> _options;
    private readonly ILogger<JwtAccessTokenService> _logger;
    private readonly TimeProvider _timeProvider;
    // MapInboundClaims maps short JWT names back to long ClaimTypes.* on validation, so
    // downstream code (e.g. ClaimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)) keeps working.
    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = true };
    private SymmetricSecurityKey? _ephemeralDevKey;

    /// <summary>
    /// Creates a new instance of the <see cref="JwtAccessTokenService"/>.
    /// </summary>
    public JwtAccessTokenService(
        IOptionsMonitor<WopiSecurityOptions> options,
        ILogger<JwtAccessTokenService> logger,
        TimeProvider? timeProvider = null)
    {
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public Task<WopiAccessToken> IssueAsync(WopiAccessTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = _options.CurrentValue;
        var now = _timeProvider.GetUtcNow();
        var lifetime = request.Lifetime ?? options.DefaultTokenLifetime;
        var expires = now.Add(lifetime);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, request.UserId),
            new(WopiClaimTypes.ResourceId, request.ResourceId),
            new(WopiClaimTypes.ResourceType, request.ResourceType.ToString()),
        };

        if (!string.IsNullOrEmpty(request.UserDisplayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, request.UserDisplayName));
            claims.Add(new Claim(WopiClaimTypes.UserDisplayName, request.UserDisplayName));
        }
        if (!string.IsNullOrEmpty(request.UserEmail))
        {
            claims.Add(new Claim(ClaimTypes.Email, request.UserEmail));
        }

        if (request.ResourceType == WopiResourceType.File && request.FilePermissions != WopiFilePermissions.None)
        {
            claims.Add(new Claim(WopiClaimTypes.FilePermissions, request.FilePermissions.ToString()));
        }
        else if (request.ResourceType == WopiResourceType.Container && request.ContainerPermissions != WopiContainerPermissions.None)
        {
            claims.Add(new Claim(WopiClaimTypes.ContainerPermissions, request.ContainerPermissions.ToString()));
        }

        if (request.AdditionalClaims is not null)
        {
            foreach (var (type, value) in request.AdditionalClaims)
            {
                claims.Add(new Claim(type, value));
            }
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Issuer = options.Issuer,
            Audience = options.Audience,
            SigningCredentials = new SigningCredentials(GetSigningKey(options), options.SigningAlgorithm),
        };

        var token = _handler.CreateToken(descriptor);
        var tokenString = _handler.WriteToken(token);
        return Task.FromResult(new WopiAccessToken(tokenString, expires));
    }

    /// <inheritdoc/>
    public Task<WopiAccessTokenValidationResult> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(WopiAccessTokenValidationResult.Failure("Token is empty."));
        }

        var options = _options.CurrentValue;
        var validationKeys = new List<SecurityKey> { GetSigningKey(options) };
        foreach (var extraKey in options.AdditionalValidationKeys)
        {
            validationKeys.Add(new SymmetricSecurityKey(extraKey));
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = validationKeys,
            ValidateLifetime = true,
            ValidateIssuer = !string.IsNullOrEmpty(options.Issuer),
            ValidIssuer = options.Issuer,
            ValidateAudience = !string.IsNullOrEmpty(options.Audience),
            ValidAudience = options.Audience,
            ClockSkew = options.ClockSkew,
            NameClaimType = ClaimTypes.NameIdentifier,
        };

        try
        {
            var principal = _handler.ValidateToken(token, parameters, out _);
            return Task.FromResult(WopiAccessTokenValidationResult.Success(principal));
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogDebug(ex, "Access token rejected: {Reason}", ex.Message);
            return Task.FromResult(WopiAccessTokenValidationResult.Failure(ex.Message));
        }
    }

    private SecurityKey GetSigningKey(WopiSecurityOptions options)
    {
        if (options.SecurityKey is not null)
        {
            return options.SecurityKey;
        }
        if (options.SigningKey is { Length: > 0 })
        {
            return new SymmetricSecurityKey(options.SigningKey);
        }
        // No key configured: generate a per-process random key. Loud warning so this is
        // never silently used in production.
        if (_ephemeralDevKey is null)
        {
            var keyBytes = RandomNumberGenerator.GetBytes(64);
            _ephemeralDevKey = new SymmetricSecurityKey(keyBytes);
            _logger.LogWarning(
                "WopiSecurityOptions.SigningKey is not configured; generated an ephemeral in-memory key. " +
                "Tokens will be invalidated on restart and cannot work across multiple host instances. " +
                "Configure Wopi:Security:SigningKey for any non-development scenario.");
        }
        return _ephemeralDevKey;
    }

    /// <summary>
    /// Helper that converts an arbitrary string secret to bytes suitable for HMAC-SHA256
    /// (right-pads to the minimum 32-byte / 256-bit length).
    /// </summary>
    public static byte[] DeriveHmacKey(string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        var raw = System.Text.Encoding.UTF8.GetBytes(secret);
        if (raw.Length >= 32)
        {
            return raw;
        }
        var padded = new byte[32];
        Array.Copy(raw, padded, raw.Length);
        return padded;
    }

}
