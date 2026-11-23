using System.ComponentModel.DataAnnotations;

namespace WopiHost.Web.Oidc.Models;

/// <summary>
/// Represents the IdP-agnostic OIDC configuration, bound from the <c>Oidc</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// The same shape works for Microsoft Entra, Auth0, Okta, Keycloak, Google, and any other
/// OpenID Connect compliant identity provider. See the project README for per-IdP examples.
/// </remarks>
public class OidcOptions
{
    /// <summary>
    /// Specifies the configuration section path this options class binds to. Use with
    /// <c>builder.Configuration.GetSection(OidcOptions.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Oidc";

    /// <summary>Gets or sets the OIDC issuer URL. The discovery document is fetched from <c>{Authority}/.well-known/openid-configuration</c>.</summary>
    [Required]
    public required string Authority { get; set; }

    /// <summary>Gets or sets the OAuth2 client identifier issued by the IdP.</summary>
    [Required]
    public required string ClientId { get; set; }

    /// <summary>Gets or sets the OAuth2 client secret. Optional for public clients that use PKCE.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Gets or sets the path the IdP redirects back to after sign-in. The default <c>/signin-oidc</c> matches ASP.NET Core convention.</summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>Gets or sets the path the IdP redirects back to after sign-out. The default <c>/signout-callback-oidc</c> matches ASP.NET Core convention.</summary>
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>Gets or sets the scopes to request from the IdP. Defaults to <c>openid profile email</c>.</summary>
    public IList<string> Scopes { get; set; } = ["openid", "profile", "email"];

    /// <summary>Gets or sets a value indicating whether to use PKCE. Defaults to <see langword="true"/>, as OAuth 2.1 recommends for all clients.</summary>
    public bool UsePkce { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the metadata endpoint must use HTTPS. Defaults to <see langword="true"/>; set it to <see langword="false"/> only for local mock servers.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Gets or sets the name of the claim that carries the user's roles. The default is <c>roles</c>; Entra uses <c>roles</c>, some IdPs use <c>groups</c>.</summary>
    public string RoleClaimType { get; set; } = "roles";
}
