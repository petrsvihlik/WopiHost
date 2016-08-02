using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Url
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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="wopiClientUrl"></param>
		/// <param name="wopiHostUrl"></param>
		public WopiUrlGenerator(string wopiClientUrl, string wopiHostUrl)
		{
			WopiClientUrl = wopiClientUrl;
			WopiHostUrl = wopiHostUrl;
			OptionalParameters.Add("ui", "en-US"); //TODO: test value
		}

		public string GetContainerUrl(string containerIdentifier, string accessToken)
		{
			return $"{WopiHostUrl}/wopi/containers/{containerIdentifier}/children?access_token={accessToken}";
		}


		public async Task<string> GetFileUrlAsync(string extension, string fileIdentifier, string accessToken, WopiActionEnum action)
		{
			var template = await WopiDiscoverer.GetUrlTemplateAsync(extension, action);
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
				url += "&access_token=" + Uri.EscapeDataString(accessToken);

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
