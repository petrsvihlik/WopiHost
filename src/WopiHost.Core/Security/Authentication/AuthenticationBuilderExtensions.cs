using Microsoft.AspNetCore.Authentication;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Provides WOPI-related extensions for <see cref="AuthenticationBuilder"/>.
/// </summary>
public static class AuthenticationBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="AccessTokenHandler"/> to the <see cref="AuthenticationBuilder"/>.
    /// </summary>
    /// <param name="builder">The authentication builder to add the scheme to.</param>
    /// <param name="authenticationScheme">The authentication scheme name.</param>
    /// <param name="displayName">The display name of the authentication scheme.</param>
    /// <param name="configureOptions">A delegate that configures the <see cref="AccessTokenAuthenticationOptions"/>.</param>
    /// <returns>The same <paramref name="builder"/> instance, for chaining.</returns>
    public static AuthenticationBuilder AddTokenAuthentication(
        this AuthenticationBuilder builder, 
        string authenticationScheme, 
        string displayName, 
        Action<AccessTokenAuthenticationOptions> configureOptions) => 
        builder.AddScheme<AccessTokenAuthenticationOptions, AccessTokenHandler>(authenticationScheme, displayName, configureOptions);
}
