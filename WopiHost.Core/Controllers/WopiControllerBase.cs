using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Controllers
{
	public abstract class WopiControllerBase : ControllerBase
	{
		protected IWopiStorageProvider StorageProvider { get; set; }

		protected IWopiSecurityHandler SecurityHandler { get; set; }

		protected IConfiguration Configuration { get; set; }

		public string BaseUrl
		{
			get
			{
				return HttpContext.Request.Scheme + "://" + HttpContext.Request.Host;
			}
		}

		protected string AccessToken
		{
			get
			{
				var authenticateInfo = HttpContext.Authentication.GetAuthenticateInfoAsync(AccessTokenDefaults.AuthenticationScheme).Result;
				return authenticateInfo?.Properties?.GetTokenValue(AccessTokenDefaults.AccessTokenQueryName);
			}
		}

		protected WopiControllerBase(IWopiStorageProvider fileProvider, IWopiSecurityHandler securityHandler, IConfiguration configuration)
		{
			StorageProvider = fileProvider;
			SecurityHandler = securityHandler;
			Configuration = configuration;
		}

		protected string GetChildUrl(string controller, string identifier, string accessToken)
		{
			identifier = Uri.EscapeDataString(identifier);
			accessToken = Uri.EscapeDataString(accessToken);
			return $"{BaseUrl}/wopi/{controller}/{identifier}?access_token={accessToken}";
		}
	}
}
