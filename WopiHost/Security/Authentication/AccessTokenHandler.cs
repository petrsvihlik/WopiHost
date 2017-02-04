using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.Extensions.Logging;

namespace WopiHost.Security
{
	public class AccessTokenHandler : AuthenticationHandler<AccessTokenAuthenticationOptions>
	{
		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			try
			{
				var token = Context.Request.Query[AccessTokenDefaults.AccessTokenQueryName];
				var principal = Options.SecurityHandler.GetPrincipal(token);

				var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), Options.AuthenticationScheme);

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

		protected override Task HandleSignInAsync(SignInContext context)
		{
			throw new NotImplementedException();
		}

		protected override Task HandleSignOutAsync(SignOutContext context)
		{
			throw new NotImplementedException();
		}
	}
}