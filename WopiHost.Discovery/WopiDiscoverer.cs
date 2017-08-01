using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery
{
    public class WopiDiscoverer : IDiscoverer
    {
        private const string ELEMENT_NET_ZONE = "net-zone";
        private const string ELEMENT_APP = "app";
        private const string ELEMENT_ACTION = "action";
        private const string ATTR_NET_ZONE_NAME = "name";
        private const string ATTR_ACTION_EXTENSION = "ext";
        private const string ATTR_ACTION_NAME = "name";
        private const string ATTR_ACTION_URL = "urlsrc";
        private const string ATTR_ACTION_REQUIRES = "requires";
        private const string ATTR_APP_NAME = "name";
        private const string ATTR_APP_FAVICON = "favIconUrl";
        private const string ATTR_VAL_COBALT = "cobalt";

        private IDiscoveryFileProvider DiscoveryFileProvider { get; }

        public NetZoneEnum NetZone { get; }


        public WopiDiscoverer(IDiscoveryFileProvider discoveryFileProvider, NetZoneEnum netZone = NetZoneEnum.Any)
        {
            DiscoveryFileProvider = discoveryFileProvider;
            NetZone = netZone;
        }

        private async Task<IEnumerable<XElement>> GetAppsAsync()
        {
            return (await DiscoveryFileProvider.GetDiscoveryXmlAsync())
                .Elements(ELEMENT_NET_ZONE)
                .Where(ValidateNetZone)
                .Elements(ELEMENT_APP);
        }

        private bool ValidateNetZone(XElement e)
        {
            if (NetZone != NetZoneEnum.Any)
            {
                var netZoneString = (string)e.Attribute(ATTR_NET_ZONE_NAME);
                netZoneString = netZoneString.Replace("-", "");
                NetZoneEnum netZone;
                bool success = Enum.TryParse(netZoneString, true, out netZone);
                return success && (netZone == NetZone);
            }
            return true;
        }

        public async Task<bool> SupportsExtensionAsync(string extension)
        {
            var query = (await GetAppsAsync()).Elements()
                .FirstOrDefault(e => (string)e.Attribute(ATTR_ACTION_EXTENSION) == extension);
            return query != null;
        }

        public async Task<bool> SupportsActionAsync(string extension, WopiActionEnum action)
        {
            string actionString = action.ToString().ToLower();

            var query = (await GetAppsAsync()).Elements().Where(e => (string) e.Attribute(ATTR_ACTION_EXTENSION) == extension && (string) e.Attribute(ATTR_ACTION_NAME) == actionString);

            return query.Any();
        }

        public async Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action)
        {
            string actionString = action.ToString().ToLower();

            var query = (await GetAppsAsync()).Elements().Where(e => (string) e.Attribute(ATTR_ACTION_EXTENSION) == extension && (string) e.Attribute(ATTR_ACTION_NAME) == actionString).Select(e => e.Attribute(ATTR_ACTION_REQUIRES).Value.Split(','));

            return query.FirstOrDefault();
        }

        public async Task<bool> RequiresCobaltAsync(string extension, WopiActionEnum action)
        {
            var requirements = await GetActionRequirementsAsync(extension, action);
            return requirements != null && requirements.Contains(ATTR_VAL_COBALT);
        }

        public async Task<string> GetUrlTemplateAsync(string extension, WopiActionEnum action)
        {
            string actionString = action.ToString().ToLower();

            var query = (await GetAppsAsync()).Elements().Where(e => (string) e.Attribute(ATTR_ACTION_EXTENSION) == extension && (string) e.Attribute(ATTR_ACTION_NAME) == actionString).Select(e => e.Attribute(ATTR_ACTION_URL).Value);

            return query.FirstOrDefault();
        }

        public async Task<string> GetApplicationNameAsync(string extension)
        {
            var query = (await GetAppsAsync()).Where(e => e.Descendants(ELEMENT_ACTION).Any(d => (string) d.Attribute(ATTR_ACTION_EXTENSION) == extension)).Select(e => e.Attribute(ATTR_APP_NAME).Value);

            return query.FirstOrDefault();
        }

        public async Task<string> GetApplicationFavIconAsync(string extension)
        {
            var query = (await GetAppsAsync()).Where(e => e.Descendants(ELEMENT_ACTION).Any(d => (string) d.Attribute(ATTR_ACTION_EXTENSION) == extension)).Select(e => e.Attribute(ATTR_APP_FAVICON).Value);

            return query.FirstOrDefault();
        }
    }
}
