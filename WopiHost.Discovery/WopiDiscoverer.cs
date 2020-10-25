using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery
{
    ///<inheritdoc cref="IDiscoverer"/>
    public class WopiDiscoverer : IDiscoverer
    {
        private const string ElementNetZone = "net-zone";
        private const string ElementApp = "app";
        private const string ElementAction = "action";
        private const string AttrNetZoneName = "name";
        private const string AttrActionExtension = "ext";
        private const string AttrActionName = "name";
        private const string AttrActionUrl = "urlsrc";
        private const string AttrActionRequires = "requires";
        private const string AttrAppName = "name";
        private const string AttrAppFavicon = "favIconUrl";
        private const string AttrValCobalt = "cobalt";

        private IEnumerable<XElement> _apps;

        private IDiscoveryFileProvider DiscoveryFileProvider { get; }

        public NetZoneEnum NetZone { get; }


        public WopiDiscoverer(IDiscoveryFileProvider discoveryFileProvider, NetZoneEnum netZone = NetZoneEnum.Any)
        {
            DiscoveryFileProvider = discoveryFileProvider;
            NetZone = netZone;
        }

        private async Task<IEnumerable<XElement>> GetAppsAsync()
        {
            if (_apps is null)
            {
                _apps = (await DiscoveryFileProvider.GetDiscoveryXmlAsync())
                    .Elements(ElementNetZone)
                    .Where(ValidateNetZone)
                    .Elements(ElementApp);
            }

            return _apps;
        }

        private bool ValidateNetZone(XElement e)
        {
            if (NetZone != NetZoneEnum.Any)
            {
                var netZoneString = (string)e.Attribute(AttrNetZoneName);
                netZoneString = netZoneString.Replace("-", "", StringComparison.InvariantCulture);
                var success = Enum.TryParse(netZoneString, true, out NetZoneEnum netZone);
                return success && (netZone == NetZone);
            }
            return true;
        }

        ///<inheritdoc />
        public async Task<bool> SupportsExtensionAsync(string extension)
        {
            var query = (await GetAppsAsync()).Elements()
                .FirstOrDefault(e => (string)e.Attribute(AttrActionExtension) == extension);
            return query is not null;
        }

        ///<inheritdoc />
        public async Task<bool> SupportsActionAsync(string extension, WopiActionEnum action)
        {
            var actionString = action.ToString().ToLowerInvariant();

            var query = (await GetAppsAsync()).Elements().Where(e => (string)e.Attribute(AttrActionExtension) == extension && e.Attribute(AttrActionName).Value.ToLowerInvariant() == actionString);

            return query.Any();
        }

        ///<inheritdoc />
        public async Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action)
        {
            var actionString = action.ToString().ToLowerInvariant();

            var query = (await GetAppsAsync()).Elements().Where(e => (string)e.Attribute(AttrActionExtension) == extension && e.Attribute(AttrActionName).Value.ToLowerInvariant() == actionString).Select(e => e.Attribute(AttrActionRequires).Value.Split(','));

            return query.FirstOrDefault();
        }

        ///<inheritdoc />
        public async Task<bool> RequiresCobaltAsync(string extension, WopiActionEnum action)
        {
            var requirements = await GetActionRequirementsAsync(extension, action);
            return requirements is not null && requirements.Contains(AttrValCobalt);
        }

        ///<inheritdoc />
        public async Task<string> GetUrlTemplateAsync(string extension, WopiActionEnum action)
        {
            var actionString = action.ToString().ToLowerInvariant();
            var query = (await GetAppsAsync()).Elements().Where(e => (string)e.Attribute(AttrActionExtension) == extension && e.Attribute(AttrActionName).Value.ToLowerInvariant() == actionString).Select(e => e.Attribute(AttrActionUrl).Value);
            return query.FirstOrDefault();
        }

        ///<inheritdoc />
        public async Task<string> GetApplicationNameAsync(string extension)
        {
            var query = (await GetAppsAsync()).Where(e => e.Descendants(ElementAction).Any(d => (string)d.Attribute(AttrActionExtension) == extension)).Select(e => e.Attribute(AttrAppName).Value);

            return query.FirstOrDefault();
        }

        ///<inheritdoc />
        public async Task<Uri> GetApplicationFavIconAsync(string extension)
        {
            var query = (await GetAppsAsync()).Where(e => e.Descendants(ElementAction).Any(d => (string)d.Attribute(AttrActionExtension) == extension)).Select(e => e.Attribute(AttrAppFavicon).Value);
            var result = query.FirstOrDefault();
            return result is not null ? new Uri(result) : null;
        }
    }
}
