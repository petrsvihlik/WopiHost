using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Http.Features.Authentication;

namespace WopiHost.Security
{
	public class AccessTokenHandler : AuthenticationHandler<AccessTokenAuthenticationOptions>
	{
		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			// get from Context.Request

			try
			{
				//TODO: get principal from token validator.ValidateToken(token, validationParameters, out validatedToken);
				//https://github.com/aspnet/Security/tree/master/src/Microsoft.AspNetCore.Authentication.JwtBearer
				var token = Context.Request.Query["access_token"];

				var principal = new ClaimsPrincipal();
				principal.AddIdentity(new ClaimsIdentity(new List<Claim>
				{
					new Claim(ClaimTypes.NameIdentifier, "12345"),
					new Claim(ClaimTypes.Name, "Anonymous"),
					new Claim(ClaimTypes.Email, "anonymous@domain.tld")
				}));

				var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), Options.AuthenticationScheme);

				if (Options.SaveToken)
				{
					ticket.Properties.StoreTokens(new[]
					{
						new AuthenticationToken { Name = "access_token", Value = token }
					});
				}
				return Task.FromResult(AuthenticateResult.Success(ticket));
			}
			catch (Exception ex)
			{
				//TODO:log
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