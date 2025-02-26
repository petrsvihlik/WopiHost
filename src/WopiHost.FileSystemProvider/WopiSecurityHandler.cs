﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <inheritdoc/>
/// <summary>
/// Creates a new instance of the <see cref="WopiSecurityHandler"/>.
/// </summary>
/// <param name="loggerFactory">An instance of a type used to configure the logging system and create instances of Microsoft.Extensions.Logging.ILogger from the registered Microsoft.Extensions.Logging.ILoggerProviders.</param>
public class WopiSecurityHandler(ILoggerFactory loggerFactory) : IWopiSecurityHandler
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<WopiSecurityHandler>();
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private SymmetricSecurityKey? _key = null;

    private SymmetricSecurityKey Key
    {
        get
        {
            if (_key is null)
            {
                //RandomNumberGenerator rng = RandomNumberGenerator.Create();
                //byte[] key = new byte[128];
                //rng.GetBytes(key);
                var key = Encoding.ASCII.GetBytes("secretKeysecretKeysecretKey123"/* + new Random(DateTime.Now.Millisecond).Next(1,999)*/);
                _key = new SymmetricSecurityKey(key);
            }

            return _key;
        }
    }

    //TODO: abstract
    private readonly Dictionary<string, ClaimsPrincipal> _userDatabase = new()
    {
        {
            "Anonymous",
            new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new(ClaimTypes.NameIdentifier, "12345"),
                new(ClaimTypes.Name, "Anonymous"),
                new(ClaimTypes.Email, "anonymous@domain.tld"),

                //TDOO: this needs to be done per file
                new(WopiClaimTypes.USER_PERMISSIONS, (WopiUserPermissions.UserCanWrite | WopiUserPermissions.UserCanRename | WopiUserPermissions.UserCanAttend | WopiUserPermissions.UserCanPresent).ToString())
            ])
        )
        }
    };

    /// <inheritdoc/>
    public SecurityToken GenerateAccessToken(string userId, string resourceId)
    {
        var user = _userDatabase[userId];

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = user.Identities.FirstOrDefault(),
            Expires = DateTime.UtcNow.AddHours(1), //access token ttl: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/concepts#the-access_token_ttl-property
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
        };

        return _tokenHandler.CreateToken(tokenDescriptor);
    }

    /// <inheritdoc/>
    public ClaimsPrincipal? GetPrincipal(string token)
    {
        //TODO: https://github.com/aspnet/Security/tree/master/src/Microsoft.AspNetCore.Authentication.JwtBearer

        var tokenValidation = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateActor = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = Key
        };

        try
        {
            // Try to validate the token
            return _tokenHandler.ValidateToken(token, tokenValidation, out var _);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(ex.HResult), ex, ex.Message);
            return null;
        }
    }

    /// <inheritdoc/>
    public bool IsAuthorized(ClaimsPrincipal principal, string resourceId, WopiAuthorizationRequirement operation) =>
        //TODO: logic
        true;

    /// <summary>
    /// Converts the security token to a Base64 string.
    /// </summary>
    public string WriteToken(SecurityToken token) => _tokenHandler.WriteToken(token);
}
