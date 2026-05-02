using System.ComponentModel.DataAnnotations;

namespace WopiHost.Web.Oidc.Models;

/// <summary>
/// IdP-agnostic OIDC configuration. Bind from the <c>Oidc</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// The same shape works for Microsoft Entra, Auth0, Okta, Keycloak, Google, and any other
/// OpenID Connect compliant identity provider. See the project README for per-IdP examples.
/// </remarks>
public class OidcOptions
{
    /// <summary>OIDC issuer URL. Discovery document is fetched from <c>{Authority}/.well-known/openid-configuration</c>.</summary>
    [Required]
    public required string Authority { get; set; }

    /// <summary>OAuth2 client identifier issued by the IdP.</summary>
    [Required]
    public required string ClientId { get; set; }

    /// <summary>OAuth2 client secret. Optional for confidential clients using PKCE.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Path the IdP redirects back to after sign-in. Default <c>/signin-oidc</c> matches ASP.NET Core convention.</summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>Path the IdP redirects back to after sign-out. Default <c>/signout-callback-oidc</c> matches ASP.NET Core convention.</summary>
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>Scopes to request from the IdP. Defaults to <c>openid profile email</c>.</summary>
    public IList<string> Scopes { get; set; } = ["openid", "profile", "email"];

    /// <summary>Whether to use PKCE. Default true; recommended for all clients per OAuth 2.1.</summary>
    public bool UsePkce { get; set; } = true;

    /// <summary>Whether to require HTTPS for the metadata endpoint. Default true; set false only for local mock servers.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Name of the claim that carries the user's roles. Default <c>roles</c>; Entra uses <c>roles</c>, some IdPs use <c>groups</c>.</summary>
    public string RoleClaimType { get; set; } = "roles";
}
