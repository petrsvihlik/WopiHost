﻿using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;
using WopiHost.Validator.Models;

namespace WopiHost.Validator.Infrastructure;

/// <inheritdoc/>
/// <summary>
/// Creates a new instance of the <see cref="WopiSecurityHandler"/>.
/// </summary>
/// <param name="loggerFactory">An instance of a type used to configure the logging system and create instances of Microsoft.Extensions.Logging.ILogger from the registered Microsoft.Extensions.Logging.ILoggerProviders.</param>
public class WopiSecurityHandler(IOptions<WopiOptions> options) : IWopiSecurityHandler
{
    /// <inheritdoc/>
    public Task<WopiUserPermissions> GetUserPermissions(ClaimsPrincipal principal, IWopiFile file, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WopiUserPermissions.UserCanWrite |
            WopiUserPermissions.UserCanRename |
            WopiUserPermissions.UserCanAttend |
            WopiUserPermissions.UserCanPresent);
    }

    /// <inheritdoc/>
    public Task<SecurityToken> GenerateAccessToken(string userId, string resourceId, CancellationToken cancellationToken = default)
    {
        var token = new NonSecureSecurityToken(userId);
        return Task.FromResult<SecurityToken>(token);
    }

    /// <inheritdoc/>
    public Task<ClaimsPrincipal?> GetPrincipal(string token, CancellationToken cancellationToken = default)
    {
        if (token != options.Value.UserId)
        {
            return Task.FromResult<ClaimsPrincipal?>(null);
        }
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, options.Value.UserId),
            new(ClaimTypes.Name, options.Value.UserId),
            new(ClaimTypes.Email, options.Value.UserId + "@domain.tld")
        };

        return Task.FromResult<ClaimsPrincipal?>(
            new ClaimsPrincipal(
                new ClaimsIdentity(claims, AccessTokenDefaults.AUTHENTICATION_SCHEME)));
    }

    /// <inheritdoc/>
    public Task<bool> IsAuthorized(ClaimsPrincipal principal, IWopiAuthorizationRequirement requirement, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(principal.Identity?.IsAuthenticated == true);
    }

    /// <summary>
    /// Converts the security token to a Base64 string.
    /// </summary>
    public string WriteToken(SecurityToken token) => token.Id;
}

public class NonSecureSecurityToken : SecurityToken
{
    private readonly string _id;
    private readonly string _userName;
    private readonly DateTime _effectiveTime;

    public NonSecureSecurityToken(string userName)
    {
        _userName = userName;
        _id = userName;// Guid.NewGuid().ToString("N");
        _effectiveTime = DateTime.UtcNow;
    }

    public override string Id
    {
        get { return _id; }
    }

    public override DateTime ValidFrom
    {
        get { return _effectiveTime; }
    }

    public override DateTime ValidTo
    {
        // Never expire
        get { return DateTime.MaxValue; }
    }

    public string UserName
    {
        get { return _userName; }
    }

    public override string Issuer => throw new NotImplementedException();

    public override SecurityKey SecurityKey => throw new NotImplementedException();

    public override SecurityKey SigningKey { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
}