using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Authenticates requests by reading the WOPI <c>access_token</c> from the query string, validating
/// it via <see cref="IWopiAccessTokenService"/>, and exposing the resulting
/// <see cref="System.Security.Claims.ClaimsPrincipal"/> on the request.
/// </summary>
/// <param name="accessTokenService">The service that validates the access token.</param>
/// <param name="options">The monitor for the handler options.</param>
/// <param name="logger">The logger factory.</param>
/// <param name="encoder">The URL encoder.</param>
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
    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.Request.Path.Value?.StartsWith("/wopi", StringComparison.OrdinalIgnoreCase) != true)
        {
            return AuthenticateResult.NoResult();
        }

        var token = Context.Request.Query[AccessTokenDefaults.AccessTokenQueryName].ToString();
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.Fail("Access token missing.");
        }

        var validation = await accessTokenService.ValidateAsync(token, Context.RequestAborted).ConfigureAwait(false);
        if (!validation.IsValid || validation.Principal is null)
        {
            LogTokenValidationFailed(Logger, validation.FailureReason);
            return AuthenticateResult.Fail(validation.FailureReason ?? "Invalid access token.");
        }

        var ticket = new AuthenticationTicket(validation.Principal, new AuthenticationProperties(), Scheme.Name);
        if (Options.SaveToken)
        {
            ticket.Properties.StoreTokens([new AuthenticationToken { Name = AccessTokenDefaults.AccessTokenQueryName, Value = token }]);
        }
        return AuthenticateResult.Success(ticket);
    }
}
