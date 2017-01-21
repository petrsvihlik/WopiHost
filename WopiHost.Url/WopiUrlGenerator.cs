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
		private readonly IDiscoveryFileProvider _discoveryFileProvider;

		private WopiDiscoverer WopiDiscoverer => new WopiDiscoverer(_discoveryFileProvider);


		public WopiUrlSettings UrlSettings { get; }

		/// <summary>
		/// Creates a new instance of WOPI URL generator class.
		/// </summary>
		/// <param name="discoveryFileProvider">Object providing WOPI discovery XML.</param>
		/// <param name="urlSettings">Additional settings influencing behavior of the WOPI client.</param>
		public WopiUrlGenerator(IDiscoveryFileProvider discoveryFileProvider, WopiUrlSettings urlSettings = null)
		{
			_discoveryFileProvider = discoveryFileProvider;
			UrlSettings = urlSettings;
		}

		/// <summary>
		/// Generates an URL for a given file and action.
		/// </summary>
		/// <param name="extension">File extension used to identify a correct URL template.</param>
		/// <param name="wopiFileUrl">URL of a file.</param>
		/// <param name="action">Action used to identify a correct URL template.</param>
		/// <param name="urlSettings">Additional URL settings (if not specified, defaults passed to the class constructor will be used).</param>
		/// <returns></returns>
		public async Task<string> GetFileUrlAsync(string extension, string wopiFileUrl, WopiActionEnum action, WopiUrlSettings urlSettings = null)
		{
			var combinedUrlSettings = new WopiUrlSettings(urlSettings.Merge(UrlSettings));
			var template = await WopiDiscoverer.GetUrlTemplateAsync(extension, action);
			if (template != null)
			{
				// Resolve optional parameters
				var url = Regex.Replace(template, @"<(?<name>\w*)=(?<value>\w*)&*>", m => ResolveOptionalParameter(m.Groups["name"].Value, m.Groups["value"].Value, combinedUrlSettings));
				url = url.TrimEnd('&');

				// Append mandatory parameters
				url += "&WOPISrc=" + Uri.EscapeDataString(wopiFileUrl);

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
