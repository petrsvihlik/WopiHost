using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WopiHost.Core.Security.Authentication
{
	public class AccessTokenHandler : AuthenticationHandler<AccessTokenAuthenticationOptions>
	{
		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			try
			{
                //TODO: implement access_token_ttl https://msdn.microsoft.com/en-us/library/hh695362(v=office.12).aspx		

			    var token = Context.Request.Query[AccessTokenDefaults.AccessTokenQueryName];

                if (Context.Request.Path.Value == "/wopibootstrapper")
                {
                    //TODO: Implement properly: http://wopi.readthedocs.io/projects/wopirest/en/latest/bootstrapper/Bootstrap.html
                    //Should be removed or replaced with bearer token check
                    token = Options.SecurityHandler.WriteToken(Options.SecurityHandler.GenerateAccessToken("Anonymous", Convert.ToBase64String(Encoding.UTF8.GetBytes(".\\"))));

                }
				var principal = Options.SecurityHandler.GetPrincipal(token);

				var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), Scheme.Name);
                
                if (Options.SaveToken)
				{
					ticket.Properties.StoreTokens(new[]
					{
						new AuthenticationToken { Name = AccessTokenDefaults.AccessTokenQueryName, Value = token }
					});
				}
                return Task.FromResult(AuthenticateResult.Success(ticket));
			}
            catch (Exception ex)
			{
				Logger.LogError(new EventId(ex.HResult), ex, ex.Message);
				return Task.FromResult(AuthenticateResult.Fail(ex));
			}
		}
        

	    public AccessTokenHandler(IOptionsMonitor<AccessTokenAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
	    {
	    }
	}
}