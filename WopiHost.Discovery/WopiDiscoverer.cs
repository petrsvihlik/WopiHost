using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery
{
	public class WopiDiscoverer
	{
		private const string ELEMENT_NET_ZONE = "net-zone";
		private const string ELEMENT_APP = "app";
		private const string ELEMENT_ACTION = "action";
		private const string ATTR_ACTION_EXTENSION = "ext";
		private const string ATTR_ACTION_NAME = "name";
		private const string ATTR_ACTION_URL = "urlsrc";
		private const string ATTR_ACTION_REQUIRES = "requires";
		private const string ATTR_APP_NAME = "name";
		private const string ATTR_APP_FAVICON = "favIconUrl";
		private const string ATTR_VAL_COBALT = "cobalt";

		private IDiscoveryFileProvider DiscoveryFileProvider { get; set; }


		public WopiDiscoverer(IDiscoveryFileProvider discoveryFileProvider)
		{
			DiscoveryFileProvider = discoveryFileProvider;
		}

		public async Task<bool> SupportsExtensionAsync(string extension)
		{
			var query = (await DiscoveryFileProvider.GetDiscoveryXmlAsync()).Elements(ELEMENT_NET_ZONE).Elements(ELEMENT_APP).Elements()
				.FirstOrDefault(e => (string)e.Attribute(ATTR_ACTION_EXTENSION) == extension);
			return query != null;
		}

		public async Task<bool> SupportsActionAsync(string extension, WopiActionEnum action)
		{
			string actionString = action.ToString().ToLower();

			var query = from e in (await DiscoveryFileProvider.GetDiscoveryXmlAsync()).Elements(ELEMENT_NET_ZONE).Elements(ELEMENT_APP).Elements()
						where (string)e.Attribute(ATTR_ACTION_EXTENSION) == extension && (string)e.Attribute(ATTR_ACTION_NAME) == actionString
						select e;

			return query.Any();
		}

		public async Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action)
		{
			string actionString = action.ToString().ToLower();

			var query = from e in (await DiscoveryFileProvider.GetDiscoveryXmlAsync()).Elements(ELEMENT_NET_ZONE).Elements(ELEMENT_APP).Elements()
						where (string)e.Attribute(ATTR_ACTION_EXTENSION) == extension && (string)e.Attribute(ATTR_ACTION_NAME) == actionString
						select e.Attribute(ATTR_ACTION_REQUIRES).Value.Split(',');

			return query.SingleOrDefault();
		}

		public async Task<bool> RequiresCobaltAsync(string extension, WopiActionEnum action)
		{
			var requirements = await GetActionRequirementsAsync(extension, action);
			return requirements != null && requirements.Contains(ATTR_VAL_COBALT);
		}

		public async Task<string> GetUrlTemplateAsync(string extension, WopiActionEnum action)
		{
			string actionString = action.ToString().ToLower();

			var query = from e in (await DiscoveryFileProvider.GetDiscoveryXmlAsync()).Elements(ELEMENT_NET_ZONE).Elements(ELEMENT_APP).Elements()
						where (string)e.Attribute(ATTR_ACTION_EXTENSION) == extension && (string)e.Attribute(ATTR_ACTION_NAME) == actionString
						select e.Attribute(ATTR_ACTION_URL).Value;

			return query.SingleOrDefault();
		}

		public async Task<string> GetApplicationNameAsync(string extension)
		{
			var query = from e in (await DiscoveryFileProvider.GetDiscoveryXmlAsync()).Elements(ELEMENT_NET_ZONE).Elements(ELEMENT_APP)
						where e.Descendants(ELEMENT_ACTION).Any(d => (string)d.Attribute(ATTR_ACTION_EXTENSION) == extension)
						select e.Attribute(ATTR_APP_NAME).Value;

			return query.SingleOrDefault();
		}

		public async Task<string> GetApplicationFavIconAsync(string extension)
		{
			var query = from e in (await DiscoveryFileProvider.GetDiscoveryXmlAsync()).Elements(ELEMENT_NET_ZONE).Elements(ELEMENT_APP)
						where e.Descendants(ELEMENT_ACTION).Any(d => (string)d.Attribute(ATTR_ACTION_EXTENSION) == extension)
						select e.Attribute(ATTR_APP_FAVICON).Value;

			return query.SingleOrDefault();
		}
	}
}
