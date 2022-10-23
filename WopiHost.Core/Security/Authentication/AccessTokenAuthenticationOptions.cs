using Microsoft.AspNetCore.Authentication;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Configuration object for <see cref="AccessTokenHandler"/>.
/// </summary>
public class AccessTokenAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Defines whether the token should be stored in the <see cref="Microsoft.AspNetCore.Authentication.AuthenticationProperties"/> after a successful authorization.
    /// </summary>
    public bool SaveToken { get; set; } = true;

    /// <summary>
    /// Performs AuthNZ operations. 
    /// </summary>
    public IWopiSecurityHandler SecurityHandler { get; set; }
}
