using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Web.Oidc.Controllers;

/// <summary>
/// Sign-in and sign-out endpoints. Sign-in challenges the OIDC scheme so the user is
/// bounced to their IdP; sign-out clears the cookie and (best-effort) hits the IdP's
/// end-session endpoint for single-logout.
/// </summary>
[AllowAnonymous]
[Route("account")]
public class AccountController : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/",
        };
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var properties = new AuthenticationProperties { RedirectUri = "/" };
        return SignOut(properties,
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("denied")]
    public IActionResult Denied() => View();
}
