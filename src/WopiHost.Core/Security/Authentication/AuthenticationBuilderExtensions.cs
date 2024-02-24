using Microsoft.AspNetCore.Authentication;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// WOPI-related extensions for <see cref="AuthenticationBuilder"/>.
/// </summary>
public static class AuthenticationBuilderExtensions
	{
    /// <summary>
    /// Adds <see cref="AccessTokenHandler"/> to the <see cref="AuthenticationBuilder"/>.
    /// </summary>
    /// <param name="builder">An instance of <see cref="AuthenticationBuilder"/></param>
    /// <param name="authenticationScheme">Schema name</param>
    /// <param name="displayName">Schema display name</param>
    /// <param name="configureOptions">A delegate for configuring <see cref="AccessTokenAuthenticationOptions"/></param>
    /// <returns></returns>
    public static AuthenticationBuilder AddTokenAuthentication(this AuthenticationBuilder builder, string authenticationScheme, string displayName, Action<AccessTokenAuthenticationOptions> configureOptions) => builder.AddScheme<AccessTokenAuthenticationOptions, AccessTokenHandler>(authenticationScheme, displayName, configureOptions);
}
