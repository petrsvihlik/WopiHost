using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Url
{
	/// <summary>
	/// Generates WOPI URLs according to the specification
	/// WOPI v2 spec: http://wopi.readthedocs.io/en/latest/discovery.html
	/// WOPI v1 spec: https://msdn.microsoft.com/en-us/library/hh695362(v=office.12).aspx
	/// </summary>
	public class WopiUrlGenerator
	{
		private WopiDiscoverer _wopiDiscoverer;

		private WopiDiscoverer WopiDiscoverer
		{
			get { return _wopiDiscoverer ?? (_wopiDiscoverer = new WopiDiscoverer(WopiClientUrl)); }
		}

		public string WopiClientUrl { get; }
		public string WopiHostUrl { get; set; }

		public WopiUrlSettings UrlSettings { get; }

		/// <summary>
		/// Creates a new instance of WOPI URL generator class.
		/// </summary>
		/// <param name="wopiClientUrl">URL of the WOPI client (OWA/OOS/etc.)</param>
		/// <param name="wopiHostUrl">URL of the WOPI host (endpoint serving the content to WOPI client).</param>
		/// <param name="urlSettings">Additional settings influencing behavior of the WOPI client.</param>
		public WopiUrlGenerator(string wopiClientUrl, string wopiHostUrl, WopiUrlSettings urlSettings = null)
		{
			WopiClientUrl = wopiClientUrl;
			WopiHostUrl = wopiHostUrl;
			UrlSettings = urlSettings;
		}

		public string GetContainerUrl(string containerIdentifier, string accessToken)
		{
			containerIdentifier = Uri.EscapeDataString(containerIdentifier);
			accessToken = Uri.EscapeDataString(accessToken);
			return $"{WopiHostUrl}/wopi/containers/{containerIdentifier}/children?access_token={accessToken}";
		}

		/// <summary>
		/// Generates an URL for a given file and action.
		/// </summary>
		/// <param name="extension">File extension used to identify a correct URL template.</param>
		/// <param name="fileIdentifier">Identifier of a file which an object of interest.</param>
		/// <param name="accessToken">Access token that will be added to the URL.</param>
		/// <param name="action">Action used to identify a correct URL template.</param>
		/// <param name="urlSettings">Additional URL settings (if not specified, defaults passed to the class constructor will be used).</param>
		/// <returns></returns>
		public async Task<string> GetFileUrlAsync(string extension, string fileIdentifier, string accessToken, WopiActionEnum action, WopiUrlSettings urlSettings = null)
		{
			var combinedUrlSettings = new WopiUrlSettings(urlSettings.Merge(UrlSettings));
			var template = await WopiDiscoverer.GetUrlTemplateAsync(extension, action);
			if (template != null)
			{
				// Resolve optional parameters
				var url = Regex.Replace(template, @"<(?<name>\w*)=(?<value>\w*)&*>", m => ResolveOptionalParameter(m.Groups["name"].Value, m.Groups["value"].Value, combinedUrlSettings));
				url = url.TrimEnd('&');

				// Append mandatory parameters
				var fileUrl = WopiHostUrl + "/wopi/files/" + fileIdentifier;
				url += "&WOPISrc=" + Uri.EscapeDataString(fileUrl);
				url += "&access_token=" + Uri.EscapeDataString(accessToken);

				return url;
			}
			return null;
		}

		private string ResolveOptionalParameter(string name, string value, WopiUrlSettings urlSettings)
		{
			string param = null;
			if (urlSettings.TryGetValue(value, out param))
			{
				return name + "=" + Uri.EscapeDataString(param) + "&";
			}
			return null;
		}
	}
}
