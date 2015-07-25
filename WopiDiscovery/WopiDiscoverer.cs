using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using WopiDiscovery.Enumerations;

namespace WopiDiscovery
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


        private XElement _discoveryXml;
		public string WopiClientUrl { get; }

		public XElement DiscoveryXml
		{
			get { return _discoveryXml ?? (_discoveryXml = XElement.Load(WopiClientUrl + "/hosting/discovery")); }
		}


		public WopiDiscoverer(string wopiClientUrl)
		{
			WopiClientUrl = wopiClientUrl;
		}

		public bool SupportsAction(string extension, WopiActionEnum action)
		{
			string actionString = action.ToString().ToLower();

			var query = from e in DiscoveryXml.Elements(ELEMENT_NET_ZONE).Elements(ELEMENT_APP).Elements()
						where (string)e.Attribute(ATTR_ACTION_EXTENSION) == extension && (string)e.Attribute(ATTR_ACTION_NAME) == actionString
						select e;

			return query.Any();
		}

		public IEnumerable<string> GetActionRequirements(string extension, WopiActionEnum action)
		{
			string actionString = action.ToString().ToLower();

			var query = from e in DiscoveryXml.Elements(ELEMENT_NET_ZONE).Elements(ELEMENT_APP).Elements()
						where (string)e.Attribute(ATTR_ACTION_EXTENSION) == extension && (string)e.Attribute(ATTR_ACTION_NAME) == actionString
						select e.Attribute(ATTR_ACTION_REQUIRES).Value.Split(',');

			return query.SingleOrDefault();
		}

	    public bool RequiresCobalt(string extension, WopiActionEnum action)
	    {
	        return GetActionRequirements(extension, action).Contains(ATTR_VAL_COBALT);
	    }

		public string GetUrlTemplate(string extension, WopiActionEnum action)
		{
			string actionString = action.ToString().ToLower();

			var query = from e in DiscoveryXml.Elements(ELEMENT_NET_ZONE).Elements(ELEMENT_APP).Elements()
						where (string)e.Attribute(ATTR_ACTION_EXTENSION) == extension && (string)e.Attribute(ATTR_ACTION_NAME) == actionString
						select e.Attribute(ATTR_ACTION_URL).Value;

			return query.SingleOrDefault();
		}

		public string GetApplicationName(string extension)
		{
			var query = from e in DiscoveryXml.Elements(ELEMENT_NET_ZONE).Elements(ELEMENT_APP)
						where e.Descendants(ELEMENT_ACTION).Any(d => (string)d.Attribute(ATTR_ACTION_EXTENSION) == extension)
						select e.Attribute(ATTR_APP_NAME).Value;

			return query.SingleOrDefault();
		}

		public string GetApplicationFavIcon(string extension)
		{
			var query = from e in DiscoveryXml.Elements(ELEMENT_NET_ZONE).Elements(ELEMENT_APP)
						where e.Descendants(ELEMENT_ACTION).Any(d => (string)d.Attribute(ATTR_ACTION_EXTENSION) == extension)
						select e.Attribute(ATTR_APP_FAVICON).Value;

			return query.SingleOrDefault();
		}
	}
}
