using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Class facilitating authentication using an access token query parameter.
/// </summary>
public class AccessTokenHandler : AuthenticationHandler<AccessTokenAuthenticationOptions>
{
    /// <summary>
    /// Handles authentication using the access_token query parameter.
    /// </summary>
    /// <returns><see cref="AuthenticateResult"/> set to <see cref="AuthenticateResult.Succeeded"/> when the token is valid and <see cref="AuthenticateResult.Failure"/> when the token is invalid or expired.</returns>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            //TODO: implement access_token_ttl https://msdn.microsoft.com/en-us/library/hh695362(v=office.12).aspx		

            var token = Context.Request.Query[AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME].ToString();

            if (Context.Request.Path.Value == "/wopibootstrapper")
            {
                //TODO: Implement properly: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/bootstrap
                //Should be removed or replaced with bearer token check
                token = Options.SecurityHandler.WriteToken(Options.SecurityHandler.GenerateAccessToken("Anonymous", Convert.ToBase64String(Encoding.UTF8.GetBytes(".\\"))));
            }

            if (!string.IsNullOrEmpty(token))
            {
                var principal = Options.SecurityHandler.GetPrincipal(token);

                if (principal != null)
                {
                    var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), Scheme.Name);

                    if (Options.SaveToken)
                    {
                        ticket.Properties.StoreTokens(new[]
                        {
                            new AuthenticationToken { Name = AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME, Value = token }
                        });
                    }
                    return Task.FromResult(AuthenticateResult.Success(ticket));
                }
                else
                {
                    string message = "Principal not found.";
                    Logger.LogInformation(message);
                    return Task.FromResult(AuthenticateResult.Fail(message));
                }
            }
            else
            {
                string message = "Token not found.";
                Logger.LogInformation(message);
                return Task.FromResult(AuthenticateResult.Fail(message));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(new EventId(ex.HResult), ex, ex.Message);
            return Task.FromResult(AuthenticateResult.Fail(ex));
        }
    }

    /// <summary>
    /// Creates an instance of <see cref="AccessTokenHandler"/>.
    /// </summary>
    /// <param name="options">The monitor for the options instance.</param>
    /// <param name="logger">The Microsoft.Extensions.Logging.ILoggerFactory.</param>
    /// <param name="encoder">The System.Text.Encodings.Web.UrlEncoder.</param>
    /// <param name="clock">The Microsoft.AspNetCore.Authentication.ISystemClock.</param>
    public AccessTokenHandler(IOptionsMonitor<AccessTokenAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
    {
        // Used by for Dependency Injection
    }
}