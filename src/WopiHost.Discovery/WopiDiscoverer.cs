using System.Xml.Linq;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery;

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

    private AsyncExpiringLazy<IEnumerable<XElement>> _apps;

    private IDiscoveryFileProvider DiscoveryFileProvider { get; }

    private DiscoveryOptions DiscoveryOptions { get; }

    private AsyncExpiringLazy<IEnumerable<XElement>> Apps
    {
        get
        {
            return _apps ??= new AsyncExpiringLazy<IEnumerable<XElement>>(async metadata =>
            {
                return new TemporaryValue<IEnumerable<XElement>>
                {
                    Result = (await DiscoveryFileProvider.GetDiscoveryXmlAsync())
                    .Elements(ElementNetZone)
                    .Where(ValidateNetZone)
                    .Elements(ElementApp),

                    ValidUntil = DateTimeOffset.UtcNow.Add(DiscoveryOptions.RefreshInterval)
                };
            });
        }
    }

    /// <summary>
    /// Creates a new instance of the <see cref="WopiDiscoverer"/>, a class for examining the capabilities of the WOPI client.
    /// </summary>
    /// <param name="discoveryFileProvider">A service that provides the discovery file to examine.</param>
    /// <param name="discoveryOptions"></param>
    public WopiDiscoverer(IDiscoveryFileProvider discoveryFileProvider, DiscoveryOptions discoveryOptions)
    {
        DiscoveryFileProvider = discoveryFileProvider;
        DiscoveryOptions = discoveryOptions;
    }

    internal async Task<IEnumerable<XElement>> GetAppsAsync()
    {
        return await Apps.Value();
    }

    private bool ValidateNetZone(XElement e)
    {
        var netZoneString = (string)e.Attribute(AttrNetZoneName);
        netZoneString = netZoneString.Replace("-", "", StringComparison.InvariantCulture);
        var success = Enum.TryParse(netZoneString, true, out NetZoneEnum netZone);
        return success && (netZone == DiscoveryOptions.NetZone);
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
        var actionString = action.ToString().ToUpperInvariant();

        var query = (await GetAppsAsync()).Elements().Where(e => (string)e.Attribute(AttrActionExtension) == extension && e.Attribute(AttrActionName).Value.ToUpperInvariant() == actionString);

        return query.Any();
    }

    ///<inheritdoc />
    public async Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action)
    {
        var actionString = action.ToString().ToUpperInvariant();

        var query = (await GetAppsAsync()).Elements().Where(e => (string)e.Attribute(AttrActionExtension) == extension && e.Attribute(AttrActionName).Value.ToUpperInvariant() == actionString).Select(e => e.Attribute(AttrActionRequires).Value.Split(','));

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
        var actionString = action.ToString().ToUpperInvariant();
        var query = (await GetAppsAsync()).Elements().Where(e => (string)e.Attribute(AttrActionExtension) == extension && e.Attribute(AttrActionName).Value.ToUpperInvariant() == actionString).Select(e => e.Attribute(AttrActionUrl).Value);
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
