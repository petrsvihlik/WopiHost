using Microsoft.AspNetCore.Authentication;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Represents the options for <see cref="AccessTokenHandler"/>.
/// </summary>
public class AccessTokenAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the access token is stored in the
    /// <see cref="AuthenticationProperties"/> after successful authentication. The default is <see langword="true"/>.
    /// </summary>
    public bool SaveToken { get; set; } = true;
}
