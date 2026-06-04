using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace WopiHost.Web.Oidc.Endpoints;

/// <summary>
/// Sign-in and sign-out endpoints. Razor Components static SSR can't issue auth challenges from
/// component code, so the OIDC scheme is invoked via <see cref="Results.Challenge"/> /
/// <see cref="Results.SignOut"/>.
/// </summary>
internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/account/login", (string? returnUrl) =>
        {
            // Open-redirect guard: only honour returnUrl when it's a relative reference.
            // Url.IsLocalUrl (the legacy MVC helper) isn't available outside MVC; the
            // documented standalone equivalent is Uri.IsWellFormedUriString with
            // UriKind.Relative, which rejects absolute URIs and malformed inputs alike.
            var props = new AuthenticationProperties
            {
                RedirectUri = !string.IsNullOrEmpty(returnUrl)
                    && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
                    ? returnUrl
                    : "/",
            };
            return Results.Challenge(props, [OpenIdConnectDefaults.AuthenticationScheme]);
        }).AllowAnonymous();

        // Accept both GET and POST so the sign-out link works whether triggered by the
        // layout's POST form (anti-forgery-protected) or by a direct GET from manual nav.
        endpoints.MapMethods("/account/logout", ["GET", "POST"], () =>
        {
            var props = new AuthenticationProperties { RedirectUri = "/" };
            return Results.SignOut(props,
                [CookieAuthenticationDefaults.AuthenticationScheme,
                 OpenIdConnectDefaults.AuthenticationScheme]);
        }).AllowAnonymous();
    }
}
