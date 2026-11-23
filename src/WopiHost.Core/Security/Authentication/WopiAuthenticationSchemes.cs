namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Provides the authentication scheme names used by the WOPI host.
/// </summary>
public static class WopiAuthenticationSchemes
{
    /// <summary>
    /// The scheme for the <c>/wopi/*</c> endpoints, which authenticates the WOPI client's
    /// <c>access_token</c> query parameter through <see cref="AccessTokenHandler"/>.
    /// </summary>
    public const string AccessToken = AccessTokenDefaults.AuthenticationScheme;

    /// <summary>
    /// The scheme for the <c>/wopibootstrapper</c> endpoints, which authenticates an OAuth2 Bearer
    /// token from the host's identity provider, per the WOPI bootstrap spec.
    /// </summary>
    /// <remarks>
    /// Hosts that use the bootstrapper endpoints must register a scheme under this name in their
    /// authentication pipeline; otherwise no registration is required.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddAuthentication()
    ///     .AddJwtBearer(WopiAuthenticationSchemes.Bootstrap, o =&gt; { /* IdP config */ });
    /// </code>
    /// </example>
    public const string Bootstrap = "WopiBootstrap";
}
