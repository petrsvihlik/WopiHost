using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// ASP.NET Core authentication handler that reads the WOPI <c>access_token</c> from the query
/// string, validates it via <see cref="IWopiAccessTokenService"/>, and exposes the resulting
/// <see cref="System.Security.Claims.ClaimsPrincipal"/> on the request.
/// </summary>
/// <remarks>
/// Only requests under <c>/wopi</c> are handled — for any other path the handler returns
/// <see cref="AuthenticateResult.NoResult"/> so other authentication schemes can take over.
/// </remarks>
public partial class AccessTokenHandler(
    IWopiAccessTokenService accessTokenService,
    IOptionsMonitor<AccessTokenAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AccessTokenAuthenticationOptions>(options, logger, encoder)
{
    /// <summary>
    /// Validates the <c>access_token</c> query parameter on WOPI requests.
    /// </summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.Request.Path.Value?.StartsWith("/wopi", StringComparison.OrdinalIgnoreCase) != true)
        {
            return AuthenticateResult.NoResult();
        }

        var token = Context.Request.Query[AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME].ToString();
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.Fail("Access token missing.");
        }

        var validation = await accessTokenService.ValidateAsync(token, Context.RequestAborted);
        if (!validation.IsValid || validation.Principal is null)
        {
            LogTokenValidationFailed(Logger, validation.FailureReason);
            return AuthenticateResult.Fail(validation.FailureReason ?? "Invalid access token.");
        }

        var ticket = new AuthenticationTicket(validation.Principal, new AuthenticationProperties(), Scheme.Name);
        if (Options.SaveToken)
        {
            ticket.Properties.StoreTokens([new AuthenticationToken { Name = AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME, Value = token }]);
        }
        return AuthenticateResult.Success(ticket);
    }
}
