namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Authentication scheme names used by the WOPI host.
/// </summary>
public static class WopiAuthenticationSchemes
{
    /// <summary>
    /// Scheme for <c>/wopi/*</c> endpoints — the WOPI client's <c>access_token</c> query parameter.
    /// Backed by <see cref="AccessTokenHandler"/>.
    /// </summary>
    public const string AccessToken = AccessTokenDefaults.AUTHENTICATION_SCHEME;

    /// <summary>
    /// Scheme for the <c>/wopibootstrapper</c> endpoint — OAuth2 Bearer from the host's
    /// identity provider, per the WOPI bootstrap spec.
    /// </summary>
    /// <remarks>
    /// Hosts must register a corresponding scheme in their auth pipeline:
    /// <code>
    /// services.AddAuthentication()
    ///     .AddJwtBearer(WopiAuthenticationSchemes.Bootstrap, o =&gt; { /* IdP config */ });
    /// </code>
    /// If the bootstrapper endpoint is not used, no scheme registration is required.
    /// </remarks>
    public const string Bootstrap = "WopiBootstrap";
}
