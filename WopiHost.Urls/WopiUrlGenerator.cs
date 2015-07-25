using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WopiDiscovery;
using WopiDiscovery.Enumerations;
using WopiHost.Contracts;

namespace WopiHost.Urls
{
	public class WopiUrlGenerator
	{
		private WopiDiscoverer _wopiDiscoverer;
		private Dictionary<string, string> _optionalParameters = new Dictionary<string, string>();
		private WopiDiscoverer WopiDiscoverer
		{
			get { return _wopiDiscoverer ?? (_wopiDiscoverer = new WopiDiscoverer(WopiClientUrl)); }
		}

		public string WopiClientUrl { get; }
		public string WopiHostUrl { get; set; }

		public Dictionary<string, string> OptionalParameters
		{
			get { return _optionalParameters; }
		}

		public IWopiSecurityHandler SecurityHandler { get; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="wopiClientUrl"></param>
		/// <param name="wopiHostUrl"></param>
		/// <param name="securityHandler"></param>
		public WopiUrlGenerator(string wopiClientUrl, string wopiHostUrl, IWopiSecurityHandler securityHandler)
		{
			WopiClientUrl = wopiClientUrl;
			WopiHostUrl = wopiHostUrl;
			SecurityHandler = securityHandler;
			OptionalParameters.Add("ui", "en-US"); //TODO: test value
		}

		public string GetUrl(string extension, string fileIdentifier, WopiActionEnum action)
		{
			// TODO: Consider generating WOPISrc based on routes:
			// WebAPI2:	https://msdn.microsoft.com/en-us/library/system.web.http.routing.urlhelper.link.aspx
			// MVC:		https://msdn.microsoft.com/en-us/library/cc668201.aspx
			////Microsoft.AspNet.Mvc.UrlHelper u = new Microsoft.AspNet.Mvc.UrlHelper();u.Link()
			

			var template = WopiDiscoverer.GetUrlTemplate(extension, action);
			if (template != null)
			{
				int i = 0;

				// Resolve optional parameters
				var url = Regex.Replace(template, @"<(\w*)=\w*&*>", m => ResolveOptionalParameter(m.Groups[1].Value, i++));
				url = url.TrimEnd('&');
				//TODO: setup preferred ui/data culture (rs,ui)

				// Append mandatory parameters
				var fileUrl = WopiHostUrl + "/wopi/files/" + fileIdentifier;
				url += "&WOPISrc=" + Uri.EscapeDataString(fileUrl);
				url += "&access_token=" + Uri.EscapeDataString(SecurityHandler.GenerateAccessToken(fileIdentifier));
				
				return url;
			}
			return null;
		}

		private string ResolveOptionalParameter(string s, int i)
		{
			string param = null;
			if (OptionalParameters.TryGetValue(s, out param))
			{
				return s + "=" + Uri.EscapeDataString(param) + "&";
			}
			return null;
		}
	}
}
