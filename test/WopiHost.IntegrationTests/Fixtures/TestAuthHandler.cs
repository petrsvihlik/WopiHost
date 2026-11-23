using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
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

/// <summary>Provides <see cref="HttpClient"/> extensions that attach a test identity to every request a client sends.</summary>
public static class TestAuthClientExtensions
{
    /// <summary>
    /// Sets default request headers on <paramref name="client"/> that identify a test user, replacing
    /// any identity set earlier. Each test typically creates a fresh client per identity scenario.
    /// </summary>
    /// <param name="client">The client to configure.</param>
    /// <param name="sub">The subject claim of the test user.</param>
    /// <param name="name">The optional display name of the test user.</param>
    /// <param name="email">The optional email address of the test user.</param>
    /// <param name="roles">The role claims of the test user.</param>
    /// <returns>The same <paramref name="client"/>, for chaining.</returns>
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
