using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Class facilitating authentication using an access token query parameter.
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="AccessTokenHandler"/>.
/// </remarks>
/// <param name="options">The monitor for the options instance.</param>
/// <param name="securityHandler">An instance of a security handler.</param>
/// <param name="logger">The Microsoft.Extensions.Logging.ILoggerFactory.</param>
/// <param name="encoder">The System.Text.Encodings.Web.UrlEncoder.</param>
public class AccessTokenHandler(
    IWopiSecurityHandler securityHandler,
    IOptionsMonitor<AccessTokenAuthenticationOptions> options, 
    ILoggerFactory logger, 
    UrlEncoder encoder) : AuthenticationHandler<AccessTokenAuthenticationOptions>(options, logger, encoder)
{
    /// <summary>
    /// Handles authentication using the access_token query parameter.
    /// </summary>
    /// <returns><see cref="AuthenticateResult"/> set to <see cref="AuthenticateResult.Succeeded"/> when the token is valid and <see cref="AuthenticateResult.Failure"/> when the token is invalid or expired.</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            //TODO: implement access_token_ttl https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/adb48ba9-118a-43b6-82d7-9a508aad1583	

            if (Context.Request.Path.Value?.StartsWith("/wopi", StringComparison.OrdinalIgnoreCase) != true)
            {
                return AuthenticateResult.NoResult();
            }

            var token = Context.Request.Query[AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME].ToString();

            if (Context.Request.Path.Value == "/wopibootstrapper")
            {
                //TODO: Implement properly: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/bootstrap
                //Should be removed or replaced with bearer token check
                token = securityHandler.WriteToken(
                    await securityHandler.GenerateAccessToken("Anonymous", Convert.ToBase64String(Encoding.UTF8.GetBytes(".\\"))));
            }


            if (!string.IsNullOrEmpty(token))
            {
                var principal = await securityHandler.GetPrincipal(token);

                if (principal != null)
                {
                    var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), Scheme.Name);

                    if (Options.SaveToken)
                    {
                        ticket.Properties.StoreTokens(
                        [
                            new AuthenticationToken { Name = AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME, Value = token }
                        ]);
                    }
                    return AuthenticateResult.Success(ticket);
                }
                else
                {
                    Logger.LogError("Principal not found from token");
                    return AuthenticateResult.Fail("Principal not found.");
                }
            }
            else
            {
                Logger.LogError("Token not found in request");
                return AuthenticateResult.Fail("Token not found.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(new EventId(ex.HResult), ex, ex.Message);
            return AuthenticateResult.Fail(ex);
        }
    }
}