using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

namespace WopiHost.Core.Controllers
{
	[Route("wopibootstrapper")]
	public class WopiBootstrapperController : WopiControllerBase
	{
		public WopiBootstrapperController(IWopiStorageProvider fileProvider, IWopiSecurityHandler securityHandler, IConfiguration configuration) : base(fileProvider, securityHandler, configuration)
		{

		}

		[HttpPost]
		[Produces("application/json")]
		public IActionResult GetRootContainer()
		{
			var authorizationHeader = HttpContext.Request.Headers["Authorization"];
			var ecosystemOperation = HttpContext.Request.Headers["X-WOPI-EcosystemOperation"];
			if (ValidateAuthorizationHeader(authorizationHeader))
			{
				var accessToken = GenerateAccessToken();
				BootstrapRootContainerInfo bootstrapRoot = new BootstrapRootContainerInfo
				{
					Bootstrap = new BootstrapInfo
					{
						EcosystemUrl = "",
						SignInName = "",
						UserFriendlyName = "",
						UserId = ""
					}
				};
				if (ecosystemOperation == "GET_ROOT_CONTAINER")
				{
					//TODO: implement bootstrap + token
					bootstrapRoot.RootContainerInfo = new RootContainerInfo
					{
						ContainerPointer = new ChildContainer
						{
							Name = StorageProvider.RootContainerPointer.Name,
							Url = GetChildUrl("containers", StorageProvider.RootContainerPointer.Identifier, accessToken)
						}
					};
				}
				else if (ecosystemOperation == "GET_NEW_ACCESS_TOKEN")
				{
					//TODO: set expiration
					bootstrapRoot.AccessTokenInfo = new AccessTokenInfo
					{
						AccessToken = accessToken,
						AccessTokenExpiry = 0
					};
				}
				else
				{
					return new NotImplementedResult();
				}
				return new JsonResult(bootstrapRoot);
			}
			else
			{
				//TODO: implement WWW-authentication header https://wopirest.readthedocs.io/en/latest/bootstrapper/Bootstrap.html#www-authenticate-header
				string authorizationUri = "https://contoso.com/api/oauth2/authorize";
				string tokenIssuanceUri = "https://contoso.com/api/oauth2/token";
				string providerId = "tp_contoso";
				string urlSchemes = Uri.EscapeDataString("{\"iOS\" : [\"contoso\",\"contoso - EMM\"], \"Android\" : [\"contoso\",\"contoso - EMM\"], \"UWP\": [\"contoso\",\"contoso - EMM\"]}");
				Response.Headers.Add("WWW-Authenticate", $"Bearer authorization_uri=\"{authorizationUri}\",tokenIssuance_uri=\"{tokenIssuanceUri}\",providerId=\"{providerId}\", UrlSchemes=\"{urlSchemes}\"");
				return new UnauthorizedResult();
			}
		}

		private string GenerateAccessToken()
		{
			var now = DateTime.UtcNow;

			var claims = new Claim[]
			{
				new Claim(JwtRegisteredClaimNames.Sub, "anonymous"),
				new Claim(JwtRegisteredClaimNames.Email, "name@domain.tld"),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
				new Claim(JwtRegisteredClaimNames.Iat, DateTimeToUnixTimestamp(now).ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
			};

			// Create the JWT and write it to a string
			var jwt = new JwtSecurityToken("todo", "", claims, now, now.Add(new TimeSpan(365,0,0)));
			var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
			return encodedJwt;
		}

		public static double DateTimeToUnixTimestamp(DateTime dateTime)
		{
			DateTime unixStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
			long unixTimeStampInTicks = (dateTime.ToUniversalTime() - unixStart).Ticks;
			return (double)unixTimeStampInTicks / TimeSpan.TicksPerSecond;
		}

		private bool ValidateAuthorizationHeader(StringValues authorizationHeader)
		{
			//TODO: implement header validation http://wopi.readthedocs.io/projects/wopirest/en/latest/bootstrapper/GetRootContainer.html#sample-response
			// http://stackoverflow.com/questions/31948426/oauth-bearer-token-authentication-is-not-passing-signature-validation
			return true;
		}
	}
}
