using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WopiHost.Abstractions;

namespace WopiHost.Controllers
{
	public class WopiControllerBase : ControllerBase
	{
		public IWopiStorageProvider StorageProvider { get; set; }

		public IWopiSecurityHandler SecurityHandler { get; set; }

		public IConfiguration Configuration { get; set; }

		public string BaseUrl
		{
			get
			{
				return HttpContext.Request.Scheme + "://" + HttpContext.Request.Host;
			}
		}

		public WopiControllerBase(IWopiStorageProvider fileProvider, IWopiSecurityHandler securityHandler, IConfiguration configuration)
		{
			StorageProvider = fileProvider;
			SecurityHandler = securityHandler;
			Configuration = configuration;
		}

		public string GetChildUrl(string controller, string identifier, string accessToken)
		{
			identifier = Uri.EscapeDataString(identifier);
			accessToken = Uri.EscapeDataString(accessToken);
			return $"{BaseUrl}/wopi/{controller}/{identifier}?access_token={accessToken}";
		}
	}
}
