using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// Authentication handler that replaces the OIDC + cookie pipeline with a fixed identity for
/// integration tests that don't need to exercise the IdP handshake. The principal is encoded
/// into request headers by <see cref="TestAuthClientExtensions.AsUser"/> on each call.
/// </summary>
/// <remarks>
/// Header-based identity (rather than AsyncLocal ambient state) is the standard pattern for
/// ASP.NET Core integration tests because <see cref="HttpClient"/> calls into TestServer can
/// cross task boundaries that don't always flow <see cref="AsyncLocal{T}"/> correctly.
/// </remarks>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestAuth";
    internal const string SubHeader = "X-Test-User-Sub";
    internal const string NameHeader = "X-Test-User-Name";
    internal const string EmailHeader = "X-Test-User-Email";
    internal const string RolesHeader = "X-Test-User-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sub = Request.Headers[SubHeader].ToString();
        if (string.IsNullOrEmpty(sub))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, sub));
        identity.AddClaim(new Claim("sub", sub));

        var name = Request.Headers[NameHeader].ToString();
        if (!string.IsNullOrEmpty(name))
        {
            identity.AddClaim(new Claim("name", name));
            identity.AddClaim(new Claim(ClaimTypes.Name, name));
        }
        var email = Request.Headers[EmailHeader].ToString();
        if (!string.IsNullOrEmpty(email))
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, email));
        }
        var roles = Request.Headers[RolesHeader].ToString();
        if (!string.IsNullOrEmpty(roles))
        {
            foreach (var role in roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                identity.AddClaim(new Claim("roles", role));
            }
        }

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), new AuthenticationProperties(), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>HttpClient extensions to attach a test identity to a single request or session.</summary>
public static class TestAuthClientExtensions
{
    /// <summary>
    /// Attaches headers identifying a test user. Apply once per <see cref="HttpClient"/> instance
    /// (each test typically creates a fresh client per identity scenario).
    /// </summary>
    public static HttpClient AsUser(this HttpClient client, string sub, string? name = null, string? email = null, params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.SubHeader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        if (!string.IsNullOrEmpty(name))
        {
            client.DefaultRequestHeaders.Remove(TestAuthHandler.NameHeader);
            client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, name);
        }
        if (!string.IsNullOrEmpty(email))
        {
            client.DefaultRequestHeaders.Remove(TestAuthHandler.EmailHeader);
            client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);
        }
        if (roles is { Length: > 0 })
        {
            client.DefaultRequestHeaders.Remove(TestAuthHandler.RolesHeader);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        }
        return client;
    }
}
