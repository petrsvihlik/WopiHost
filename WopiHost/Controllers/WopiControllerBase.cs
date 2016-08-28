using System;
using System.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WopiHost.Abstractions;
using WopiHost.Url;

namespace WopiHost.Controllers
{
	public class WopiControllerBase : ControllerBase
	{
		public WopiUrlGenerator _urlGenerator;
		public IWopiFileProvider FileProvider { get; set; }

		public IWopiSecurityHandler SecurityHandler { get; set; }

		public IConfiguration Configuration { get; set; }

		public string BaseUrl
		{
			get
			{
				return HttpContext.Request.Scheme + Uri.SchemeDelimiter + HttpContext.Request.Host;
			}
		}

		public WopiUrlGenerator UrlGenerator
		{
			get { return _urlGenerator ?? (_urlGenerator = new WopiUrlGenerator(Configuration.GetSection("WopiClientUrl").Value, BaseUrl)); }
		}

		public WopiControllerBase(IWopiFileProvider fileProvider, IWopiSecurityHandler securityHandler, IConfiguration configuration)
		{
			FileProvider = fileProvider;
			SecurityHandler = securityHandler;
			Configuration = configuration;
		}
	}
}
